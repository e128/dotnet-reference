using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ListGetRangeAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128084";

    private const string ListMetadataName = "System.Collections.Generic.List`1";
    private const string CollectionsMarshalMetadataName = "System.Runtime.InteropServices.CollectionsMarshal";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use CollectionsMarshal.AsSpan with Slice instead of List.GetRange",
        "List<T>.GetRange allocates an intermediate list; use CollectionsMarshal.AsSpan(list).Slice(start, count).ToArray()",
        "Performance",
        DiagnosticSeverity.Warning,
        true,
        "List<T>.GetRange(start, count) allocates an intermediate List<T>. " +
        "CollectionsMarshal.AsSpan(list).Slice(start, count).ToArray() copies directly from the internal array.");

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
            var marshalType = compilationContext.Compilation.GetTypeByMetadataName(CollectionsMarshalMetadataName);
            if (marshalType is null)
            {
                return;
            }

            var listType = compilationContext.Compilation.GetTypeByMetadataName(ListMetadataName);
            if (listType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, listType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol listType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.Text, "GetRange", StringComparison.Ordinal))
        {
            return;
        }

        if (invocation.ArgumentList.Arguments.Count != 2)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (methodSymbol.ContainingType is not INamedTypeSymbol containingType || !containingType.IsGenericType)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, listType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }
}
