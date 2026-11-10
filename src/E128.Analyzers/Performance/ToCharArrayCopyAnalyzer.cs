using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ToCharArrayCopyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128212";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "ToCharArray copies a string only to index or iterate it",
        "'ToCharArray' allocates a copy — index the string directly or use 'AsSpan'",
        "E128-Performance",
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

        context.RegisterCompilationStartAction(static startContext =>
        {
            var stringType = startContext.Compilation.GetSpecialType(SpecialType.System_String);
            if (stringType is null)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, stringType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol stringType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !string.Equals(access.Name.Identifier.ValueText, "ToCharArray", StringComparison.Ordinal)
            || invocation.ArgumentList.Arguments.Count is not 0)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, stringType))
        {
            return;
        }

        if (!IsIndexedOrIterated(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsIndexedOrIterated(ExpressionSyntax expression)
    {
        var current = expression;
        while (current.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized;
        }

        return current.Parent switch
        {
            ForEachStatementSyntax forEach => ReferenceEquals(forEach.Expression, current),
            ElementAccessExpressionSyntax elementAccess => ReferenceEquals(elementAccess.Expression, current),
            _ => false
        };
    }
}
