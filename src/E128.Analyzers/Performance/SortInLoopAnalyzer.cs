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

    private const string ListMetadataName = "System.Collections.Generic.List`1";
    private const string ArrayMetadataName = "System.Array";
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    private static readonly DiagnosticDescriptor SortRule = new(
        DiagnosticId,
        "Sort operation inside loop creates O(n² log n) complexity",
        "'{0}' inside a loop re-sorts on every iteration — sort once before the loop or use an ordered collection",
        "Performance",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor InsertAtZeroRule = new(
        InsertAtZeroDiagnosticId,
        "List.Insert(0, ...) in loop creates O(n²) complexity",
        "Insert(0, ...) shifts all elements on every call — use LinkedList<T> or collect and reverse",
        "Performance",
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
        [SortRule, InsertAtZeroRule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
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

        if (!IsInsideLoop(invocation))
        {
            return;
        }

        if (string.Equals(methodName, "Insert", StringComparison.Ordinal))
        {
            AnalyzeInsertAtZero(context, invocation, listType);
            return;
        }

        if (!SortMethods.Contains(methodName))
        {
            return;
        }

        if (string.Equals(methodName, "Sort", StringComparison.Ordinal))
        {
            var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
            if (symbol is not IMethodSymbol method)
            {
                return;
            }

            var containingType = method.ContainingType?.OriginalDefinition;
            if (!IsSameType(containingType, listType) && !IsSameType(containingType, arrayType))
            {
                return;
            }
        }
        else
        {
            var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
            if (symbol is not IMethodSymbol method || !IsSameType(method.ContainingType, enumerableType))
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(SortRule, invocation.GetLocation(), methodName));
    }

    private static void AnalyzeInsertAtZero(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol? listType)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 2)
        {
            return;
        }

        if (args[0].Expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.NumericLiteralExpression)
            || literal.Token.Value is not int value
            || value != 0)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!IsSameType(method.ContainingType?.OriginalDefinition, listType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(InsertAtZeroRule, invocation.GetLocation()));
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

    private static bool IsInsideLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is ForEachStatementSyntax or ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                return true;
            }

            if (current is MethodDeclarationSyntax or ConstructorDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsSameType(ITypeSymbol? type, INamedTypeSymbol? target)
    {
        return target is not null && SymbolEqualityComparer.Default.Equals(type, target);
    }
}
