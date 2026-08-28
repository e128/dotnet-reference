using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IndexOfAnyToSearchValuesAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128102";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use SearchValues<char> for IndexOfAny scans",
        "string.IndexOfAny/LastIndexOfAny scans char by char without SIMD — use SearchValues<char> with a span IndexOfAny",
        "Performance",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!IsIndexOfAnyName(memberAccess.Name.Identifier.ValueText))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.ContainingType?.SpecialType != SpecialType.System_String)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsIndexOfAnyName(string name)
    {
        return name is "IndexOfAny" or "LastIndexOfAny";
    }
}
