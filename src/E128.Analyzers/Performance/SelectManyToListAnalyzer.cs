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
        "Performance",
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

        context.RegisterCompilationStartAction(compilationContext =>
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

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerableType)
    {
        var toListInvocation = (InvocationExpressionSyntax)context.Node;

        if (toListInvocation.Expression is not MemberAccessExpressionSyntax toListAccess)
        {
            return;
        }

        if (!string.Equals(toListAccess.Name.Identifier.Text, "ToList", StringComparison.Ordinal))
        {
            return;
        }

        if (toListInvocation.ArgumentList.Arguments.Count != 0)
        {
            return;
        }

        if (toListAccess.Expression is not InvocationExpressionSyntax selectManyInvocation)
        {
            return;
        }

        if (selectManyInvocation.Expression is not MemberAccessExpressionSyntax selectManyAccess)
        {
            return;
        }

        if (!string.Equals(selectManyAccess.Name.Identifier.Text, "SelectMany", StringComparison.Ordinal))
        {
            return;
        }

        if (selectManyInvocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(toListInvocation, context.CancellationToken).Symbol is not IMethodSymbol toListSymbol)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(toListSymbol.ContainingType, enumerableType))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(selectManyInvocation, context.CancellationToken).Symbol is not IMethodSymbol selectManySymbol)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(selectManySymbol.ContainingType, enumerableType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, toListInvocation.GetLocation()));
    }
}
