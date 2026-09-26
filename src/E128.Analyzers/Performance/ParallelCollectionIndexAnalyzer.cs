using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128106: Reports an element access that indexes one collection with an index bounded by the length of a
///     different collection, as in <c lang="csharp">Enumerable.Range(0, columns.Count).Select(i =&gt; rows[i])</c>.
///     A ragged pair throws <c lang="csharp">IndexOutOfRangeException</c> on the first short collection.
/// </summary>
/// <remarks>
///     The check stays syntactic on purpose. Two shapes are covered: an index that comes from an
///     <c lang="csharp">Enumerable.Range(0, X.Count)</c> lambda parameter, and an index declared by a
///     <c lang="csharp">for</c> loop whose condition reads <c lang="csharp">X.Count</c>. A relational comparison
///     against the indexed collection's own length anywhere in the same scope counts as a guard.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ParallelCollectionIndexAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128106";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Index a collection with a bound taken from a different collection",
        "'{0}' is indexed with a bound taken from '{1}', so a shorter collection throws IndexOutOfRangeException",
        "Performance",
        DiagnosticSeverity.Warning,
        true,
        "An index bounded by the length of one collection does not bound another collection. " +
        "Guard the access with a length comparison, or iterate the indexed collection itself.");

    private static readonly ImmutableHashSet<string> CountMemberNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Count",
        "Length");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ElementAccessExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var elementAccess = (ElementAccessExpressionSyntax)context.Node;

        if (elementAccess.ArgumentList is null
            || elementAccess.ArgumentList.Arguments.Count != 1
            || elementAccess.ArgumentList.Arguments[0].Expression is not IdentifierNameSyntax indexIdentifier)
        {
            return;
        }

        var indexName = indexIdentifier.Identifier.ValueText;

        if (FindBoundSource(elementAccess, indexName) is not { } boundSource)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(elementAccess.Expression, context.CancellationToken).Symbol
                is not { } indexedSymbol
            || context.SemanticModel.GetSymbolInfo(boundSource, context.CancellationToken).Symbol is not { } boundSymbol
            || SymbolEqualityComparer.Default.Equals(indexedSymbol, boundSymbol)
            || HasLengthGuard(elementAccess, indexName, indexedSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            elementAccess.GetLocation(),
            elementAccess.Expression.ToString(),
            boundSource.ToString()));
    }

    // The collection whose length bounds the index. A lambda parameter bound by
    // Enumerable.Range(0, X.Count) and a for-loop variable bounded by X.Count are the two shapes.
    private static ExpressionSyntax? FindBoundSource(ElementAccessExpressionSyntax elementAccess, string indexName)
    {
        foreach (var node in Ancestors(elementAccess))
        {
            if (node is AnonymousFunctionExpressionSyntax lambda && DeclaresParameter(lambda, indexName))
            {
                return FindRangeSource(OutermostInvocation(lambda));
            }

            if (node is ForStatementSyntax loop && DeclaresVariable(loop, indexName))
            {
                return FindConditionSource(loop);
            }

            if (node is MemberDeclarationSyntax)
            {
                return null;
            }
        }

        return null;
    }

    private static ExpressionSyntax? FindRangeSource(InvocationExpressionSyntax? invocation)
    {
        if (invocation is null)
        {
            return null;
        }

        foreach (var candidate in invocation.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (candidate.ArgumentList.Arguments.Count != 2
                || !IsZeroLiteral(candidate.ArgumentList.Arguments[0].Expression)
                || CountReceiver(candidate.ArgumentList.Arguments[1].Expression) is not { } receiver)
            {
                continue;
            }

            var methodName = candidate.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => null
            };

            if (string.Equals(methodName, "Range", StringComparison.Ordinal))
            {
                return receiver;
            }
        }

        return null;
    }

    private static ExpressionSyntax? FindConditionSource(ForStatementSyntax loop)
    {
        return loop.Condition?.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(memberAccess => CountMemberNames.Contains(memberAccess.Name.Identifier.ValueText))
            .Select(memberAccess => memberAccess.Expression)
            .FirstOrDefault();
    }

    // A length comparison against the indexed collection anywhere in the same scope makes the access safe.
    private static bool HasLengthGuard(
        ElementAccessExpressionSyntax elementAccess,
        string indexName,
        ISymbol indexedSymbol)
    {
        var scope = GuardScope(elementAccess, indexName);
        if (scope is null)
        {
            return false;
        }

        foreach (var binary in scope.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>())
        {
            if (!IsRelational(binary.Kind()))
            {
                continue;
            }

            if (MentionsLengthOf(binary.Left, indexedSymbol)
                && IsIdentifier(binary.Right, indexName))
            {
                return true;
            }

            if (MentionsLengthOf(binary.Right, indexedSymbol)
                && IsIdentifier(binary.Left, indexName))
            {
                return true;
            }
        }

        return false;
    }

    private static SyntaxNode? GuardScope(ElementAccessExpressionSyntax elementAccess, string indexName)
    {
        foreach (var node in Ancestors(elementAccess))
        {
            if (node is AnonymousFunctionExpressionSyntax lambda && DeclaresParameter(lambda, indexName))
            {
                return lambda.Body;
            }

            if (node is ForStatementSyntax loop && DeclaresVariable(loop, indexName))
            {
                return loop.Statement;
            }

            if (node is MemberDeclarationSyntax)
            {
                return null;
            }
        }

        return null;
    }

    private static IEnumerable<SyntaxNode> Ancestors(SyntaxNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            yield return parent;
        }
    }

    private static InvocationExpressionSyntax? OutermostInvocation(SyntaxNode node)
    {
        var current = node;
        var outermost = node as InvocationExpressionSyntax;

        while (current.Parent is ArgumentSyntax or ArgumentListSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax)
        {
            current = current.Parent;
            outermost = current as InvocationExpressionSyntax ?? outermost;
        }

        return outermost;
    }

    private static ExpressionSyntax? CountReceiver(ExpressionSyntax expression)
    {
        return expression is MemberAccessExpressionSyntax memberAccess
               && CountMemberNames.Contains(memberAccess.Name.Identifier.ValueText)
            ? memberAccess.Expression
            : null;
    }

    private static bool MentionsLengthOf(ExpressionSyntax expression, ISymbol indexedSymbol)
    {
        return expression is MemberAccessExpressionSyntax memberAccess
               && CountMemberNames.Contains(memberAccess.Name.Identifier.ValueText)
               && memberAccess.Expression.ToString().Length > 0
               && IsSameText(memberAccess.Expression, indexedSymbol);
    }

    private static bool IsSameText(ExpressionSyntax expression, ISymbol symbol)
    {
        return string.Equals(expression.ToString(), symbol.Name, StringComparison.Ordinal);
    }

    private static bool IsIdentifier(ExpressionSyntax expression, string name)
    {
        return expression is IdentifierNameSyntax identifier
               && string.Equals(identifier.Identifier.ValueText, name, StringComparison.Ordinal);
    }

    private static bool IsZeroLiteral(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax literal
               && string.Equals(literal.Token.ValueText, "0", StringComparison.Ordinal);
    }

    private static bool IsRelational(SyntaxKind kind)
    {
        return kind is SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression;
    }

    private static bool DeclaresParameter(AnonymousFunctionExpressionSyntax lambda, string name)
    {
        return lambda switch
        {
            SimpleLambdaExpressionSyntax simple => string.Equals(
                simple.Parameter.Identifier.ValueText,
                name,
                StringComparison.Ordinal),
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.Any(
                parameter => string.Equals(parameter.Identifier.ValueText, name, StringComparison.Ordinal)),
            _ => false
        };
    }

    private static bool DeclaresVariable(ForStatementSyntax loop, string name)
    {
        return loop.Declaration is { } declaration
               && declaration.Variables.Any(
                   variable => string.Equals(variable.Identifier.ValueText, name, StringComparison.Ordinal));
    }
}
