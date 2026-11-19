using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128243: A span obtained from <c lang="csharp">CollectionsMarshal.AsSpan</c> stays live
///     across a call that can reallocate the list's backing array.
/// </summary>
/// <remarks>
///     A growth or compaction replaces the backing array, so the old span points at memory the
///     list no longer owns. The rule reports the mutating call when a later statement in the
///     same block reads the span local. A read before the mutation is safe, and a mutation of
///     another list stays silent. A lambda and a local function body stay out, because the
///     statement order does not describe when those bodies run.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ListSpanMutationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128243";

    private const string CollectionsMarshalTypeName = "System.Runtime.InteropServices.CollectionsMarshal";

    private const string ListTypeName = "System.Collections.Generic.List`1";

    private const string SetCountName = "SetCount";

    private static readonly ImmutableHashSet<string> MutatingMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Add",
        "AddRange",
        "Clear",
        "EnsureCapacity",
        "Insert",
        "InsertRange",
        "Remove",
        "RemoveAll",
        "RemoveAt",
        "RemoveRange",
        "Reverse",
        "Sort",
        "TrimExcess");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Span over a list invalidated by a later mutation",
        "'{0}' can reallocate the list while a span over it is still live",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Reacquire the span after the mutation. A growth or compaction replaces the backing array, so the old span points at memory the list no longer owns.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            var compilation = start.Compilation;
            var collectionsMarshal = compilation.GetTypeByMetadataName(CollectionsMarshalTypeName);
            var list = compilation.GetTypeByMetadataName(ListTypeName);
            if (collectionsMarshal is null || list is null)
            {
                return;
            }

            var types = new SpanTypes(collectionsMarshal, list);
            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, types), SyntaxKind.LocalDeclarationStatement);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, SpanTypes types)
    {
        var statement = (LocalDeclarationStatementSyntax)context.Node;
        if (statement.Parent is not BlockSyntax block || statement.Declaration.Variables.Count is not 1)
        {
            return;
        }

        var declarator = statement.Declaration.Variables[0];
        if (declarator.Initializer is null
            || context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is not { } spanSymbol
            || !TryGetListSymbol(declarator.Initializer.Value, types, context, out var listSymbol)
            || listSymbol is null)
        {
            return;
        }

        var mutation = FindMutationBeforeRead(block, statement, spanSymbol, listSymbol, types, context);
        if (mutation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, mutation.GetLocation(), mutation.Expression.ToString()));
    }

    /// <summary>
    ///     True when the initializer is <c lang="csharp">CollectionsMarshal.AsSpan(list)</c> and
    ///     returns the list expression's symbol.
    /// </summary>
    private static bool TryGetListSymbol(
        ExpressionSyntax initializer,
        SpanTypes types,
        SyntaxNodeAnalysisContext context,
        out ISymbol? listSymbol)
    {
        listSymbol = null;
        if (initializer is not InvocationExpressionSyntax invocation
            || invocation.ArgumentList.Arguments.Count is 0
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method
            || !method.IsStatic
            || !string.Equals(method.Name, "AsSpan", StringComparison.Ordinal)
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, types.CollectionsMarshal))
        {
            return false;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(
            invocation.ArgumentList.Arguments[0].Expression,
            context.CancellationToken).Symbol;
        if (symbol is null)
        {
            return false;
        }

        listSymbol = symbol;
        return true;
    }

    private static InvocationExpressionSyntax? FindMutationBeforeRead(
        BlockSyntax block,
        LocalDeclarationStatementSyntax declaration,
        ISymbol spanSymbol,
        ISymbol listSymbol,
        SpanTypes types,
        SyntaxNodeAnalysisContext context)
    {
        var index = block.Statements.IndexOf(declaration);
        if (index < 0)
        {
            return null;
        }

        for (var later = index + 1; later < block.Statements.Count; later++)
        {
            var statement = block.Statements[later];
            foreach (var invocation in statement.DescendantNodesAndSelf(Descend).OfType<InvocationExpressionSyntax>())
            {
                if (!IsListMutation(invocation, listSymbol, types, context))
                {
                    continue;
                }

                return ReadsSpanAfter(block, later, invocation.SpanStart, spanSymbol, context) ? invocation : null;
            }
        }

        return null;
    }

    /// <summary>
    ///     True when the span local is read in the block at or after the mutation. A read before
    ///     the mutation leaves the span dead, so it does not report.
    /// </summary>
    private static bool ReadsSpanAfter(
        BlockSyntax block,
        int mutationStatementIndex,
        int mutationStart,
        ISymbol spanSymbol,
        SyntaxNodeAnalysisContext context)
    {
        for (var index = mutationStatementIndex; index < block.Statements.Count; index++)
        {
            foreach (var node in block.Statements[index].DescendantNodesAndSelf(Descend))
            {
                if (index == mutationStatementIndex && node.SpanStart <= mutationStart)
                {
                    continue;
                }

                if (node is IdentifierNameSyntax identifier
                    && SymbolEqualityComparer.Default.Equals(
                        context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                        spanSymbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsListMutation(
        InvocationExpressionSyntax invocation,
        ISymbol listSymbol,
        SpanTypes types,
        SyntaxNodeAnalysisContext context)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method)
        {
            return false;
        }

        if (IsSetCount(method, invocation, listSymbol, types, context))
        {
            return true;
        }

        if (method.IsStatic
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, types.List)
            || !MutatingMembers.Contains(method.Name))
        {
            return false;
        }

        return invocation.Expression is MemberAccessExpressionSyntax access
            && SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol,
                listSymbol);
    }

    private static bool IsSetCount(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        ISymbol listSymbol,
        SpanTypes types,
        SyntaxNodeAnalysisContext context)
    {
        if (!method.IsStatic
            || !string.Equals(method.Name, SetCountName, StringComparison.Ordinal)
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, types.CollectionsMarshal)
            || invocation.ArgumentList.Arguments.Count is 0)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(
            context.SemanticModel.GetSymbolInfo(
                invocation.ArgumentList.Arguments[0].Expression,
                context.CancellationToken).Symbol,
            listSymbol);
    }

    private static bool Descend(SyntaxNode node)
    {
        return node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
    }

    private sealed class SpanTypes
    {
        internal SpanTypes(INamedTypeSymbol collectionsMarshal, INamedTypeSymbol list)
        {
            CollectionsMarshal = collectionsMarshal;
            List = list;
        }

        internal INamedTypeSymbol CollectionsMarshal { get; }

        internal INamedTypeSymbol List { get; }
    }
}
