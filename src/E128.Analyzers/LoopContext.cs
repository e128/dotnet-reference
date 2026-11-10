using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers;

/// <summary>
///     Loop membership queries shared by the loop-cost rules. A node in a loop
///     header runs once, so it is not inside the loop body for cost purposes.
/// </summary>
internal static class LoopContext
{
    private enum LoopPart
    {
        Body = 0,
        Condition = 1,
        Header = 2
    }

    /// <summary>
    ///     Returns the body of the nearest enclosing loop when <paramref name="node" />
    ///     runs once per iteration. A node in a loop header, or in a lambda or local
    ///     function, returns null.
    /// </summary>
    internal static StatementSyntax? FindEnclosingLoopBody(SyntaxNode node)
    {
        return FindLoopBody(node, stopAtAnonymousFunctions: true, countLoopConditions: false);
    }

    /// <summary>
    ///     Returns true when the loop body waits between iterations. A retry or poll
    ///     loop sends one request per iteration on purpose, so it is not an N+1 loop.
    /// </summary>
    internal static bool ContainsWaitCall(
        StatementSyntax body,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
                || method.ContainingType is not { } containingType)
            {
                continue;
            }

            var ns = containingType.ContainingNamespace;
            if (string.Equals(method.Name, "Delay", StringComparison.Ordinal)
                && string.Equals(containingType.Name, "Task", StringComparison.Ordinal)
                && Namespaces.Equals(ns, "System.Threading.Tasks"))
            {
                return true;
            }

            if (string.Equals(method.Name, "Sleep", StringComparison.Ordinal)
                && string.Equals(containingType.Name, "Thread", StringComparison.Ordinal)
                && Namespaces.Equals(ns, "System.Threading"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns true when <paramref name="node" /> reads a value that changes per
    ///     iteration. A hoist of such a value is wrong, so the rule stays silent.
    /// </summary>
    internal static bool ReferencesLoopVariable(
        SyntaxNode node,
        StatementSyntax body,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        // A call inside the lookup can return a different value per iteration, so a
        // hoist would change behaviour.
        if (node.DescendantNodes().OfType<InvocationExpressionSyntax>().Any())
        {
            return true;
        }

        var varying = GetLoopLocals(body.Parent, semanticModel, cancellationToken);
        AddBodySymbols(body, semanticModel, varying, cancellationToken);
        if (varying.Count is 0)
        {
            return false;
        }

        foreach (var identifier in node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (symbol is null)
            {
                continue;
            }

            foreach (var local in varying)
            {
                if (SymbolEqualityComparer.Default.Equals(symbol, local))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns true when the nearest enclosing <see langword="for" /> loop has a
    ///     numeric literal bound. The total work is then bounded, so the cost does not
    ///     grow with input.
    /// </summary>
    internal static bool HasConstantBound(SyntaxNode node)
    {
        return FindLoopStatement(node, stopAtAnonymousFunctions: true) is ForStatementSyntax forStatement
               && HasNumericBound(forStatement.Condition);
    }

    /// <summary>
    ///     Returns the nearest enclosing loop statement, or null when the node is not
    ///     inside a loop body.
    /// </summary>
    internal static StatementSyntax? FindEnclosingLoop(SyntaxNode node)
    {
        return FindLoopBody(node, stopAtAnonymousFunctions: true, countLoopConditions: false)?.Parent as StatementSyntax;
    }

    /// <summary>
    ///     Returns true when the node runs once per iteration of an enclosing loop. A node within a
    ///     loop initializer or a foreach source runs once, so it does not count. A loop condition
    ///     and incrementor run once per iteration, so they do count.
    /// </summary>
    internal static bool IsInsideLoopBody(SyntaxNode node)
    {
        return FindLoopBody(node, stopAtAnonymousFunctions: false, countLoopConditions: true) is not null;
    }

    /// <summary>
    ///     Returns true when any enclosing loop contains the node, even when the node sits in the
    ///     loop header. A declaration boundary ends the walk.
    /// </summary>
    internal static bool IsWithinAnyLoop(SyntaxNode node)
    {
        return FindLoopStatement(node, stopAtAnonymousFunctions: false) is not null;
    }

    private static StatementSyntax? FindLoopBody(
        SyntaxNode node,
        bool stopAtAnonymousFunctions,
        bool countLoopConditions)
    {
        var loop = FindLoopStatement(node, stopAtAnonymousFunctions);
        return loop is null
            ? null
            : FindLoopBodyIn(loop, node, stopAtAnonymousFunctions, countLoopConditions);
    }

    private static StatementSyntax? FindLoopBodyIn(
        StatementSyntax loop,
        SyntaxNode node,
        bool stopAtAnonymousFunctions,
        bool countLoopConditions)
    {
        var body = GetLoopBody(loop);
        return body is null
            ? null
            : ClassifyPart(loop, node.Span) switch
            {
                LoopPart.Body => body,
                LoopPart.Condition when countLoopConditions => body,
                LoopPart.Condition => FindLoopBody(loop, stopAtAnonymousFunctions, countLoopConditions),

                // A node in a loop header runs once per outer iteration, so the outer loop decides.
                LoopPart.Header => FindLoopBody(loop, stopAtAnonymousFunctions, countLoopConditions),
                _ => null
            };
    }

    private static LoopPart ClassifyPart(StatementSyntax loop, TextSpan span)
    {
        return GetLoopBody(loop) is { } body && body.Span.Contains(span)
            ? LoopPart.Body
            : ClassifyHeader(loop, span);
    }

    private static LoopPart ClassifyHeader(StatementSyntax loop, TextSpan span)
    {
        return loop switch
        {
            ForStatementSyntax forStatement => ToHeaderPart(
                ContainsSpan(forStatement.Condition, span)
                || forStatement.Incrementors.Any(inc => inc.Span.Contains(span))),
            ForEachStatementSyntax forEach => ToHeaderPart(forEach.Identifier.Span.Contains(span)),
            ForEachVariableStatementSyntax forEachVariable => ToHeaderPart(forEachVariable.Variable.Span.Contains(span)),
            WhileStatementSyntax whileStatement => ToHeaderPart(ContainsSpan(whileStatement.Condition, span)),
            DoStatementSyntax doStatement => ToHeaderPart(ContainsSpan(doStatement.Condition, span)),
            _ => LoopPart.Header
        };
    }

    private static LoopPart ToHeaderPart(bool isCondition)
    {
        return isCondition ? LoopPart.Condition : LoopPart.Header;
    }

    private static bool ContainsSpan(SyntaxNode? node, TextSpan span)
    {
        return node is not null && node.Span.Contains(span);
    }

    private static StatementSyntax? FindLoopStatement(SyntaxNode node, bool stopAtAnonymousFunctions)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (IsBoundary(current, stopAtAnonymousFunctions))
            {
                return null;
            }

            if (current is StatementSyntax statement && GetLoopBody(statement) is not null)
            {
                return statement;
            }
        }

        return null;
    }

    private static StatementSyntax? GetLoopBody(StatementSyntax statement)
    {
        return statement switch
        {
            ForStatementSyntax forStatement => forStatement.Statement,
            CommonForEachStatementSyntax forEach => forEach.Statement,
            WhileStatementSyntax whileStatement => whileStatement.Statement,
            DoStatementSyntax doStatement => doStatement.Statement,
            _ => null
        };
    }

    private static bool HasNumericBound(ExpressionSyntax? condition)
    {
        if (condition is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        var isAscending = binary.IsKind(SyntaxKind.LessThanExpression)
                          || binary.IsKind(SyntaxKind.LessThanOrEqualExpression);
        var isBoundComparison = isAscending
                                || binary.IsKind(SyntaxKind.GreaterThanExpression)
                                || binary.IsKind(SyntaxKind.GreaterThanOrEqualExpression);
        var bound = isAscending ? binary.Right : binary.Left;
        return isBoundComparison && IsNumericLiteral(bound);
    }

    private static bool IsNumericLiteral(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax literal
               && literal.IsKind(SyntaxKind.NumericLiteralExpression);
    }

    private static bool IsBoundary(SyntaxNode node, bool stopAtAnonymousFunctions)
    {
        return node is MethodDeclarationSyntax
            or ConstructorDeclarationSyntax
            or DestructorDeclarationSyntax
            or OperatorDeclarationSyntax
            or ConversionOperatorDeclarationSyntax
            or LocalFunctionStatementSyntax
            or AccessorDeclarationSyntax
            || (stopAtAnonymousFunctions && node is AnonymousFunctionExpressionSyntax);
    }

    private static List<ISymbol> GetLoopLocals(
        SyntaxNode? loop,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var locals = new List<ISymbol>();
        if (loop is null)
        {
            return locals;
        }

        if (loop is ForEachStatementSyntax forEach)
        {
            AddDeclaredSymbol(forEach, semanticModel, locals, cancellationToken);
            return locals;
        }

        if (loop is ForEachVariableStatementSyntax variableStatement)
        {
            foreach (var designation in variableStatement.Variable
                         .DescendantNodesAndSelf()
                         .OfType<SingleVariableDesignationSyntax>())
            {
                AddDeclaredSymbol(designation, semanticModel, locals, cancellationToken);
            }

            return locals;
        }

        if (loop is ForStatementSyntax { Declaration: { } declaration })
        {
            foreach (var variable in declaration.Variables)
            {
                AddDeclaredSymbol(variable, semanticModel, locals, cancellationToken);
            }
        }

        return locals;
    }

    private static void AddDeclaredSymbol(
        SyntaxNode declaration,
        SemanticModel semanticModel,
        List<ISymbol> symbols,
        CancellationToken cancellationToken)
    {
        var symbol = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
        if (symbol is null)
        {
            return;
        }

        symbols.Add(symbol);
    }

    private static void AddBodySymbols(
        StatementSyntax body,
        SemanticModel semanticModel,
        List<ISymbol> symbols,
        CancellationToken cancellationToken)
    {
        foreach (var declarator in body.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            AddDeclaredSymbol(declarator, semanticModel, symbols, cancellationToken);
        }

        foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            AddTargetSymbol(assignment.Left, semanticModel, symbols, cancellationToken);
        }

        foreach (var unary in body.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>())
        {
            if (unary.IsKind(SyntaxKind.PreIncrementExpression) || unary.IsKind(SyntaxKind.PreDecrementExpression))
            {
                AddTargetSymbol(unary.Operand, semanticModel, symbols, cancellationToken);
            }
        }

        foreach (var unary in body.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>())
        {
            if (unary.IsKind(SyntaxKind.PostIncrementExpression) || unary.IsKind(SyntaxKind.PostDecrementExpression))
            {
                AddTargetSymbol(unary.Operand, semanticModel, symbols, cancellationToken);
            }
        }
    }

    private static void AddTargetSymbol(
        ExpressionSyntax target,
        SemanticModel semanticModel,
        List<ISymbol> symbols,
        CancellationToken cancellationToken)
    {
        if (target is not IdentifierNameSyntax)
        {
            return;
        }

        var symbol = semanticModel.GetSymbolInfo(target, cancellationToken).Symbol;
        if (symbol is null)
        {
            return;
        }

        symbols.Add(symbol);
    }
}
