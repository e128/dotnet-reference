using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

/// <summary>
///     Loop membership queries shared by the loop-cost rules. A node in a loop
///     header runs once, so it is not inside the loop body for cost purposes.
/// </summary>
internal static class LoopContext
{
    /// <summary>
    ///     Returns the body of the nearest enclosing loop when <paramref name="node" />
    ///     runs once per iteration. A node in a loop header, or in a lambda or local
    ///     function, returns null.
    /// </summary>
    internal static StatementSyntax? FindEnclosingLoopBody(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is CommonForEachStatementSyntax forEach)
            {
                if (forEach.Statement.Span.Contains(node.Span))
                {
                    return forEach.Statement;
                }

                continue;
            }

            if (ancestor is ForStatementSyntax forStatement)
            {
                if (forStatement.Statement.Span.Contains(node.Span))
                {
                    return forStatement.Statement;
                }

                continue;
            }

            if (ancestor is WhileStatementSyntax whileStatement)
            {
                if (whileStatement.Statement.Span.Contains(node.Span))
                {
                    return whileStatement.Statement;
                }

                continue;
            }

            if (ancestor is DoStatementSyntax doStatement)
            {
                if (doStatement.Statement.Span.Contains(node.Span))
                {
                    return doStatement.Statement;
                }

                continue;
            }

            if (IsBoundary(ancestor))
            {
                return null;
            }
        }

        return null;
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

            var ns = containingType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (string.Equals(method.Name, "Delay", StringComparison.Ordinal)
                && string.Equals(containingType.Name, "Task", StringComparison.Ordinal)
                && string.Equals(ns, "System.Threading.Tasks", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(method.Name, "Sleep", StringComparison.Ordinal)
                && string.Equals(containingType.Name, "Thread", StringComparison.Ordinal)
                && string.Equals(ns, "System.Threading", StringComparison.Ordinal))
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
        if (varying.Count == 0)
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
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is ForStatementSyntax forStatement)
            {
                return HasNumericBound(forStatement.Condition);
            }

            if (ancestor is CommonForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                return false;
            }

            if (IsBoundary(ancestor))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns the nearest enclosing loop statement, or null when the node is not
    ///     inside a loop body.
    /// </summary>
    internal static StatementSyntax? FindEnclosingLoop(SyntaxNode node)
    {
        var body = FindEnclosingLoopBody(node);
        return body?.Parent as StatementSyntax;
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

    private static bool IsBoundary(SyntaxNode node)
    {
        return node is MethodDeclarationSyntax
            or ConstructorDeclarationSyntax
            or DestructorDeclarationSyntax
            or OperatorDeclarationSyntax
            or ConversionOperatorDeclarationSyntax
            or LocalFunctionStatementSyntax
            or AccessorDeclarationSyntax
            or AnonymousFunctionExpressionSyntax;
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
        if (symbol is not null)
        {
            symbols.Add(symbol);
        }
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
        if (symbol is not null)
        {
            symbols.Add(symbol);
        }
    }
}
