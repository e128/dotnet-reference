using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128172: Flags a <c lang="csharp">catch</c> that swallows an xUnit assertion failure. A try block that calls
///     <c lang="csharp">Assert</c> and a broad catch with no rethrow turn a failing test into a passing one.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SwallowedAssertionAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128172";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not swallow an assertion failure",
        "This catch hides a failed assertion from the try block: rethrow, or narrow the exception type",
        "E128-Testing",
        DiagnosticSeverity.Warning,
        true,
        "A broad catch around an Assert call turns a failing test into a passing one. Rethrow the exception, or catch only the exception the test expects.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeTry, SyntaxKind.TryStatement);
    }

    private static void AnalyzeTry(SyntaxNodeAnalysisContext context)
    {
        var tryStatement = (TryStatementSyntax)context.Node;

        if (!tryStatement.Block.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call => IsAssertCall(call, context)))
        {
            return;
        }

        foreach (var catchClause in tryStatement.Catches)
        {
            if (CatchesAssertionFailure(catchClause, context) && !Rethrows(catchClause))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, catchClause.CatchKeyword.GetLocation()));
            }
        }
    }

    private static bool IsAssertCall(InvocationExpressionSyntax call, SyntaxNodeAnalysisContext context)
    {
        return context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol is IMethodSymbol method
               && string.Equals(method.ContainingType.ToDisplayString(), "Xunit.Assert", StringComparison.Ordinal);
    }

    private static bool CatchesAssertionFailure(CatchClauseSyntax catchClause, SyntaxNodeAnalysisContext context)
    {
        if (catchClause.Declaration is null)
        {
            return true;
        }

        var type = context.SemanticModel.GetTypeInfo(catchClause.Declaration.Type, context.CancellationToken).Type;
        var name = type?.ToDisplayString();

        return string.Equals(name, "System.Exception", StringComparison.Ordinal)
               || string.Equals(name, "Xunit.Sdk.XunitException", StringComparison.Ordinal);
    }

    private static bool Rethrows(CatchClauseSyntax catchClause)
    {
        return catchClause.Block.DescendantNodes().Any(node => node is ThrowStatementSyntax or ThrowExpressionSyntax);
    }
}
