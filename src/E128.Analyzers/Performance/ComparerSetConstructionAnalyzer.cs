using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128226: Reports a <c lang="csharp">HashSet&lt;T&gt;</c> that a comparer constructor creates
///     and a later loop fills. The collection expression
///     <c lang="csharp">[with(StringComparer.X), .. items]</c> passes the comparer as an argument
///     and fills the set in one statement.
/// </summary>
/// <remarks>
///     The rule reads one method body. A collection initializer, a field declaration, a set that
///     escapes to another method, and a set with a call outside the read-only set keep the report
///     silent. The rule carries no fix, because the rewrite moves the loop into the initializer.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ComparerSetConstructionAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128226";

    private const string HashSetTypeName = "System.Collections.Generic.HashSet";

    private const string EqualityComparerTypeName = "System.Collections.Generic.IEqualityComparer";

    private static readonly ImmutableHashSet<string> ReadMemberNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Contains",
        "TryGetValue");

    private static readonly ImmutableHashSet<string> ReadPropertyNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Count");

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Comparer set built by a loop",
        "Set '{0}' is created with a comparer and filled by a later loop. Build it with [with(comparer), .. items] instead.",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

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
        context.RegisterSyntaxNodeAction(AnalyzeCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

        if (creation.Initializer is not null
            || creation.ArgumentList is not { Arguments.Count: 1 } argumentList
            || !IsComparerHashSet(creation, model, cancellationToken)
            || !HasEqualityComparerArgument(argumentList.Arguments[0], model, cancellationToken)
            || GetTargetLocal(creation, model, cancellationToken) is not { } local
            || BlockScope.GetScope(creation) is not { } scope)
        {
            return;
        }

        if (!IsFilledByLoop(scope, local, model, cancellationToken)
            || !HasOnlyReadUses(scope, local, model, cancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.GetLocation(), local.Name));
    }

    private static bool IsComparerHashSet(
        ObjectCreationExpressionSyntax creation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return model.GetTypeInfo(creation, cancellationToken).Type is INamedTypeSymbol type
               && string.Equals(type.OriginalDefinition.ToDisplayString(), HashSetTypeName + "<T>", StringComparison.Ordinal);
    }

    private static bool HasEqualityComparerArgument(
        ArgumentSyntax argument,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return model.GetTypeInfo(argument.Expression, cancellationToken).Type is { } type
               && type.AllInterfaces.Any(static implemented =>
                   string.Equals(
                       implemented.OriginalDefinition.ToDisplayString(),
                       EqualityComparerTypeName + "<T>",
                       StringComparison.Ordinal));
    }

    private static ILocalSymbol? GetTargetLocal(
        ObjectCreationExpressionSyntax creation,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return creation.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } =>
                model.GetDeclaredSymbol(declarator, cancellationToken) as ILocalSymbol,
            AssignmentExpressionSyntax assignment when assignment.Right == creation
                                                       && assignment.Left is IdentifierNameSyntax =>
                model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol as ILocalSymbol,
            _ => null
        };
    }

    /// <summary>
    ///     True when every <c lang="csharp">Add</c> call on the set sits inside a loop. A set with
    ///     no loop has nothing to move into the collection expression.
    /// </summary>
    private static bool IsFilledByLoop(
        SyntaxNode scope,
        ILocalSymbol local,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var addCount = 0;

        foreach (var invocation in scope.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsSetMember(invocation, local, "Add", model, cancellationToken))
            {
                continue;
            }

            if (!IsInsideLoop(invocation, scope))
            {
                return false;
            }

            addCount++;
        }

        return addCount > 0;
    }

    private static bool IsInsideLoop(SyntaxNode node, SyntaxNode scope)
    {
        for (var current = node.Parent; current is not null && current != scope; current = current.Parent)
        {
            if (current is ForEachStatementSyntax or ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     True when every use of the set is an <c lang="csharp">Add</c> call or a read. An escape
    ///     to another method, a return, or a store into another member keeps the report silent.
    /// </summary>
    private static bool HasOnlyReadUses(
        SyntaxNode scope,
        ILocalSymbol local,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        foreach (var reference in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (reference.Parent is VariableDeclaratorSyntax
                || !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(reference, cancellationToken).Symbol,
                    local))
            {
                continue;
            }

            if (reference.Parent is not MemberAccessExpressionSyntax access || access.Expression != reference)
            {
                return false;
            }

            var name = access.Name.Identifier.ValueText;
            var allowed = access.Parent is InvocationExpressionSyntax
                ? string.Equals(name, "Add", StringComparison.Ordinal) || ReadMemberNames.Contains(name)
                : ReadPropertyNames.Contains(name);

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSetMember(
        InvocationExpressionSyntax invocation,
        ILocalSymbol local,
        string memberName,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return invocation.Expression is MemberAccessExpressionSyntax access
               && string.Equals(access.Name.Identifier.ValueText, memberName, StringComparison.Ordinal)
               && SymbolEqualityComparer.Default.Equals(
                   model.GetSymbolInfo(access.Expression, cancellationToken).Symbol,
                   local);
    }
}
