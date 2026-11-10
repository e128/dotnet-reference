using System;
using System.Collections.Generic;
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
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        // Two references can declare the same type, so the lookup keeps every match.
        var assertTypes = context.Compilation.GetTypesByMetadataName("Xunit.Assert");

        if (assertTypes.IsDefaultOrEmpty)
        {
            return;
        }

        var types = new KnownTypes(
            assertTypes,
            context.Compilation.GetTypeByMetadataName("System.Exception"),
            context.Compilation.GetTypesByMetadataName("Xunit.Sdk.XunitException"));

        context.RegisterSyntaxNodeAction(ctx => AnalyzeTry(ctx, types), SyntaxKind.TryStatement);
    }

    private static void AnalyzeTry(SyntaxNodeAnalysisContext context, KnownTypes types)
    {
        var tryStatement = (TryStatementSyntax)context.Node;

        if (!tryStatement.Block.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call => IsAssertCall(call, context, types, null)))
        {
            return;
        }

        foreach (var catchClause in tryStatement.Catches)
        {
            // An exception filter narrows the catch, so the catch does not swallow every failure.
            if (catchClause.Filter is null
                && CatchesAssertionFailure(catchClause, context, types)
                && !Rethrows(catchClause)
                && !CallsAssertFail(catchClause, context, types))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, catchClause.CatchKeyword.GetLocation()));
            }
        }
    }

    private static bool IsAssertCall(InvocationExpressionSyntax call, SyntaxNodeAnalysisContext context, KnownTypes types, string? methodName)
    {
        return context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol is IMethodSymbol method
               && (methodName is null || string.Equals(method.Name, methodName, StringComparison.Ordinal))
               && MetadataTypes.Contains(types.AssertTypes, method.ContainingType);
    }

    private static bool CatchesAssertionFailure(CatchClauseSyntax catchClause, SyntaxNodeAnalysisContext context, KnownTypes types)
    {
        if (catchClause.Declaration is null)
        {
            return true;
        }

        var type = context.SemanticModel.GetTypeInfo(catchClause.Declaration.Type, context.CancellationToken).Type;

        return SymbolEqualityComparer.Default.Equals(type, types.ExceptionType)
               || MetadataTypes.Contains(types.XunitExceptionTypes, type);
    }

    private static bool Rethrows(CatchClauseSyntax catchClause)
    {
        return OwnNodes(catchClause).Any(static node => node is ThrowStatementSyntax or ThrowExpressionSyntax);
    }

    private static bool CallsAssertFail(CatchClauseSyntax catchClause, SyntaxNodeAnalysisContext context, KnownTypes types)
    {
        return OwnNodes(catchClause).OfType<InvocationExpressionSyntax>().Any(call => IsAssertCall(call, context, types, "Fail"));
    }

    // A throw or a call inside a nested lambda or local function does not run when the catch runs.
    private static IEnumerable<SyntaxNode> OwnNodes(CatchClauseSyntax catchClause)
    {
        return catchClause.Block.DescendantNodes(static node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax));
    }

    private sealed class KnownTypes
    {
        internal KnownTypes(
            ImmutableArray<INamedTypeSymbol> assertTypes,
            INamedTypeSymbol? exceptionType,
            ImmutableArray<INamedTypeSymbol> xunitExceptionTypes)
        {
            AssertTypes = assertTypes;
            ExceptionType = exceptionType;
            XunitExceptionTypes = xunitExceptionTypes;
        }

        internal ImmutableArray<INamedTypeSymbol> AssertTypes { get; }

        internal INamedTypeSymbol? ExceptionType { get; }

        internal ImmutableArray<INamedTypeSymbol> XunitExceptionTypes { get; }
    }
}
