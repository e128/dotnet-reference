using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SortInLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128068";

    internal const string InsertAtZeroDiagnosticId = "E128069";

    internal const string RemoveAtZeroDiagnosticId = "E128153";

    private const string ListMetadataName = "System.Collections.Generic.List`1";
    private const string ArrayMetadataName = "System.Array";
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    private static readonly DiagnosticDescriptor SortRule = new(
        DiagnosticId,
        "Sort operation inside loop creates O(n² log n) complexity",
        "'{0}' inside a loop re-sorts on every iteration — sort once before the loop or use an ordered collection",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor InsertAtZeroRule = new(
        InsertAtZeroDiagnosticId,
        "List.Insert(0, ...) in loop creates O(n²) complexity",
        "Insert(0, ...) shifts all elements on every call — use LinkedList<T> or collect and reverse",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor RemoveAtZeroRule = new(
        RemoveAtZeroDiagnosticId,
        "List.RemoveAt(0) in loop creates O(n²) complexity",
        "RemoveAt(0) shifts all remaining elements on every call — use a Queue<T>, LinkedList<T>, or index forward instead",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

    private static readonly ImmutableHashSet<string> SortMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Sort",
        "OrderBy",
        "OrderByDescending",
        "ThenBy",
        "ThenByDescending");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [SortRule, InsertAtZeroRule, RemoveAtZeroRule];

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
            var listType = startContext.Compilation.GetTypeByMetadataName(ListMetadataName);
            var arrayType = startContext.Compilation.GetTypeByMetadataName(ArrayMetadataName);
            var enumerableType = startContext.Compilation.GetTypeByMetadataName(EnumerableMetadataName);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, listType, arrayType, enumerableType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? listType,
        INamedTypeSymbol? arrayType,
        INamedTypeSymbol? enumerableType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!TryGetMethodName(invocation, out var methodName))
        {
            return;
        }

        if (!LoopContext.IsWithinAnyLoop(invocation))
        {
            return;
        }

        if (string.Equals(methodName, "Insert", StringComparison.Ordinal))
        {
            AnalyzeShiftAtZero(context, invocation, listType, InsertAtZeroRule);
            return;
        }

        if (string.Equals(methodName, "RemoveAt", StringComparison.Ordinal))
        {
            AnalyzeShiftAtZero(context, invocation, listType, RemoveAtZeroRule);
            return;
        }

        if (!SortMethods.Contains(methodName)
            || !IsSortOnKnownType(context, invocation, methodName, listType, arrayType, enumerableType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(SortRule, invocation.GetLocation(), methodName));
    }

    // List.Sort and Array.Sort sort in place. Every other sort name must come from Enumerable.
    private static bool IsSortOnKnownType(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        string methodName,
        INamedTypeSymbol? listType,
        INamedTypeSymbol? arrayType,
        INamedTypeSymbol? enumerableType)
    {
        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol method)
        {
            return false;
        }

        if (!string.Equals(methodName, "Sort", StringComparison.Ordinal))
        {
            return TypeSymbols.Matches(method.ContainingType, enumerableType);
        }

        var containingType = method.ContainingType?.OriginalDefinition;

        return TypeSymbols.Matches(containingType, listType) || TypeSymbols.Matches(containingType, arrayType);
    }

    private static void AnalyzeShiftAtZero(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol? listType,
        DiagnosticDescriptor rule)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 1)
        {
            return;
        }

        if (args[0].Expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.NumericLiteralExpression)
            || literal.Token.Value is not int value
            || value is not 0)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!TypeSymbols.Matches(method.ContainingType?.OriginalDefinition, listType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(rule, invocation.GetLocation()));
    }

    private static bool TryGetMethodName(
        InvocationExpressionSyntax invocation,
        [NotNullWhen(true)] out string? methodName)
    {
        methodName = null;

        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        methodName = access.Name.Identifier.ValueText;
        return true;
    }
}
