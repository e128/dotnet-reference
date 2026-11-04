using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128155: Reports a mutation of a collection from inside a <c lang="csharp">foreach</c> loop
///     that enumerates that same collection. The enumerator throws
///     <c lang="csharp">InvalidOperationException</c> on the next move.
/// </summary>
/// <remarks>
///     The rule reads a direct reference to a local, field, or parameter only. A call result, an
///     object creation, and a collection expression each produce a fresh collection, so a loop over
///     one of those cannot invalidate its own enumerator. Immutable, frozen, concurrent, and string
///     receivers stay silent, because the named member returns a new value or is safe to call. A
///     mutation inside a lambda or a local function runs after the loop, so the rule does not enter
///     those bodies. The report sits on the loop source, so one loop reports once.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionMutationInEnumerationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128155";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Collection mutated during its own foreach",
        "'{0}' is modified inside the loop that enumerates it",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A foreach loop holds an enumerator over the collection. A structural change to that " +
        "collection invalidates the enumerator and the next MoveNext call throws " +
        "InvalidOperationException. Iterate a copy instead.");

    /// <summary>
    ///     The mutating member names for a mutable collection.
    /// </summary>
    private static readonly ImmutableHashSet<string> MutatingMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Add",
        "AddRange",
        "Clear",
        "Insert",
        "InsertRange",
        "Remove",
        "RemoveAll",
        "RemoveAt",
        "RemoveRange",
        "Reverse",
        "Sort");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeForEach, SyntaxKind.ForEachStatement);
        context.RegisterSyntaxNodeAction(AnalyzeForEach, SyntaxKind.ForEachVariableStatement);
    }

    private static void AnalyzeForEach(SyntaxNodeAnalysisContext context)
    {
        var statement = (CommonForEachStatementSyntax)context.Node;
        var collection = statement.Expression;
        var model = context.SemanticModel;
        var collectionSymbol = model.GetSymbolInfo(collection, context.CancellationToken).Symbol;

        if (collectionSymbol is not (ILocalSymbol or IFieldSymbol or IParameterSymbol)
            || IsImmutableOrConcurrent(collectionSymbol))
        {
            return;
        }

        var mutated = statement.Statement.DescendantNodes(IsVisibleNode).Any(node => node switch
        {
            InvocationExpressionSyntax invocation => IsMutatingCall(
                invocation, collectionSymbol, model, context.CancellationToken),
            AssignmentExpressionSyntax assignment => IsMutatingAssignment(
                assignment, collectionSymbol, model, context.CancellationToken),
            _ => false
        });

        if (mutated)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, collection.GetLocation(), collectionSymbol.Name));
        }
    }

    /// <summary>
    ///     True when the walk may enter the node. A lambda body and a local function body run after
    ///     the loop, so a mutation there cannot invalidate the enumerator.
    /// </summary>
    private static bool IsVisibleNode(SyntaxNode node)
    {
        return node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax;
    }

    /// <summary>
    ///     True for a receiver whose named member returns a new value or is safe to call during
    ///     enumeration. The types are immutable, frozen, concurrent, or <c lang="csharp">string</c>.
    /// </summary>
    private static bool IsImmutableOrConcurrent(ISymbol symbol)
    {
        var type = symbol switch
        {
            ILocalSymbol local => local.Type,
            IFieldSymbol field => field.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null
        };

        return type is not null
               && (type.SpecialType == SpecialType.System_String
                   || type.OriginalDefinition.ContainingNamespace?.ToDisplayString() is
                       "System.Collections.Immutable" or "System.Collections.Frozen"
                       or "System.Collections.Concurrent");
    }

    private static bool IsMutatingCall(
        InvocationExpressionSyntax invocation,
        ISymbol collectionSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !MutatingMethodNames.Contains(memberAccess.Name.Identifier.ValueText))
        {
            return false;
        }

        var receiverSymbol = model.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol;

        return SymbolEqualityComparer.Default.Equals(receiverSymbol, collectionSymbol);
    }

    private static bool IsMutatingAssignment(
        AssignmentExpressionSyntax assignment,
        ISymbol collectionSymbol,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (assignment.Left is not ElementAccessExpressionSyntax elementAccess)
        {
            return false;
        }

        var receiverSymbol = model.GetSymbolInfo(elementAccess.Expression, cancellationToken).Symbol;

        return SymbolEqualityComparer.Default.Equals(receiverSymbol, collectionSymbol);
    }
}
