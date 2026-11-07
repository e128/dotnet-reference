using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Testing;

/// <summary>
///     E128171: Flags an xUnit test whose whole body is <c lang="csharp">Assert.Fail(...)</c>. A red-phase stub marks
///     work that is not done, so it must not reach a commit.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommittedRedStubAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128171";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Replace the red-phase stub",
        "Test body is only Assert.Fail: write the real assertions, or remove the test",
        "E128-Testing",
        DiagnosticSeverity.Warning,
        true,
        "A test whose only statement is Assert.Fail is a red-phase stub. It fails every run and hides the real behavior. Write the test or remove it before you commit.");

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

        context.RegisterSyntaxNodeAction(ctx => AnalyzeMethod(ctx, assertTypes), SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> assertTypes)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (XunitTestHelper.GetFactOrTheoryAttribute(method) is null)
        {
            return;
        }

        var call = SoleCall(method);

        if (call is not null
            && context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol is IMethodSymbol target
            && string.Equals(target.Name, "Fail", StringComparison.Ordinal)
            && MetadataTypes.Contains(assertTypes, target.ContainingType))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, call.GetLocation()));
        }
    }

    private static InvocationExpressionSyntax? SoleCall(MethodDeclarationSyntax method)
    {
        return method.ExpressionBody is { Expression: InvocationExpressionSyntax expressionCall }
            ? expressionCall
            : method.Body is { Statements.Count: 1 } body
              && body.Statements[0] is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax statementCall }
                ? statementCall
                : null;
    }
}
