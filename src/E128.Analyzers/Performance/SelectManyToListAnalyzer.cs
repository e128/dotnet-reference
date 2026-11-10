using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SelectManyToListAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128085";

    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use foreach+AddRange instead of SelectMany.ToList",
        ".SelectMany(lambda).ToList() allocates LINQ iterators; use foreach + AddRange instead",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        ".SelectMany(lambda).ToList() creates intermediate LINQ iterator allocations and " +
        "grows the result list without known capacity. A foreach loop with AddRange avoids both.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var enumerableType = compilationContext.Compilation.GetTypeByMetadataName(EnumerableMetadataName);
            if (enumerableType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, enumerableType),
                SyntaxKind.InvocationExpression);
        });
    }

    // Returns the SelectMany call when the call has the shape source.SelectMany(x).ToList().
    private static InvocationExpressionSyntax? GetSelectManyInvocation(InvocationExpressionSyntax toListInvocation)
    {
        if (toListInvocation.Expression is not MemberAccessExpressionSyntax toListAccess
            || !string.Equals(toListAccess.Name.Identifier.Text, "ToList", StringComparison.Ordinal)
            || toListInvocation.ArgumentList.Arguments.Count is not 0
            || toListAccess.Expression is not InvocationExpressionSyntax selectManyInvocation
            || selectManyInvocation.Expression is not MemberAccessExpressionSyntax selectManyAccess)
        {
            return null;
        }

        return string.Equals(selectManyAccess.Name.Identifier.Text, "SelectMany", StringComparison.Ordinal)
            && selectManyInvocation.ArgumentList.Arguments.Count is 1
            ? selectManyInvocation
            : null;
    }

    private static bool IsEnumerableMethod(
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumerableType,
        SyntaxNodeAnalysisContext context)
    {
        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, enumerableType);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerableType)
    {
        var toListInvocation = (InvocationExpressionSyntax)context.Node;

        if (GetSelectManyInvocation(toListInvocation) is not { } selectManyInvocation
            || !IsEnumerableMethod(toListInvocation, enumerableType, context)
            || !IsEnumerableMethod(selectManyInvocation, enumerableType, context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, toListInvocation.GetLocation()));
    }
}
