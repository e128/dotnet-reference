using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

/// <summary>
///     Loop count and loop call queries shared by the collection and buffer rules.
///     A count is only returned when the source expression is a chain of names, so a
///     fix never evaluates a call twice.
/// </summary>
internal static class LoopCount
{
    /// <summary>
    ///     Returns a count expression for a <see langword="foreach" /> source or a
    ///     <see langword="for" /> condition bound, or null when the loop count is unknown.
    /// </summary>
    internal static string? TryGetCount(
        StatementSyntax loop,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return loop switch
        {
            CommonForEachStatementSyntax forEach => GetForeachCount(
                forEach.Expression,
                semanticModel,
                cancellationToken),
            ForStatementSyntax
            {
                Condition: BinaryExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.LessThanExpression
                } condition
            } => GetMemberCountText(condition.Right),
            _ => null
        };
    }

    /// <summary>
    ///     Returns the statement that follows <paramref name="declaration" /> when it is
    ///     a loop that calls one of <paramref name="methodNames" /> on
    ///     <paramref name="receiver" />. A loop with its own <see langword="break" /> can
    ///     stop early, so it returns null.
    /// </summary>
    internal static StatementSyntax? TryGetNextFillLoop(
        LocalDeclarationStatementSyntax declaration,
        ISymbol receiver,
        ImmutableArray<string> methodNames,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (declaration.Parent is not BlockSyntax block)
        {
            return null;
        }

        var index = block.Statements.IndexOf(declaration);
        if (index < 0 || index + 1 >= block.Statements.Count)
        {
            return null;
        }

        var loop = block.Statements[index + 1];
        if (HasDirectBreak(loop))
        {
            return null;
        }

        foreach (var methodName in methodNames)
        {
            if (ContainsCall(loop, receiver, methodName, semanticModel, cancellationToken))
            {
                return loop;
            }
        }

        return null;
    }

    /// <summary>
    ///     Returns true when the loop body calls <paramref name="methodName" /> on
    ///     <paramref name="receiver" />. A call inside a lambda or local function does not
    ///     run during the loop, so it is ignored.
    /// </summary>
    internal static bool ContainsCall(
        StatementSyntax loop,
        ISymbol receiver,
        string methodName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in loop
                     .DescendantNodes(descendIntoChildren: static node =>
                         node is not AnonymousFunctionExpressionSyntax
                             and not LocalFunctionStatementSyntax)
                     .OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax access
                || !string.Equals(access.Name.Identifier.ValueText, methodName, StringComparison.Ordinal)
                || access.Expression is not IdentifierNameSyntax receiverName)
            {
                continue;
            }

            var symbol = semanticModel.GetSymbolInfo(receiverName, cancellationToken).Symbol;
            if (SymbolEqualityComparer.Default.Equals(symbol, receiver))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns true when the loop body has a <see langword="break" /> that exits this
    ///     loop. A break inside a nested loop or a <see langword="switch" /> section exits
    ///     only that construct, so it does not count.
    /// </summary>
    internal static bool HasDirectBreak(StatementSyntax loop)
    {
        foreach (var breakStatement in loop.DescendantNodes().OfType<BreakStatementSyntax>())
        {
            foreach (var ancestor in breakStatement.Ancestors())
            {
                if (ReferenceEquals(ancestor, loop))
                {
                    return true;
                }

                if (ancestor is CommonForEachStatementSyntax
                    or ForStatementSyntax
                    or WhileStatementSyntax
                    or DoStatementSyntax
                    or SwitchStatementSyntax)
                {
                    break;
                }
            }
        }

        return false;
    }

    private static string? GetForeachCount(
        ExpressionSyntax source,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (!IsNameChain(source))
        {
            return null;
        }

        var type = semanticModel.GetTypeInfo(source, cancellationToken).Type;
        if (type is null)
        {
            return null;
        }

        var suffix = CountSuffix(type);
        return suffix is null ? null : string.Concat(source.ToString(), suffix);
    }

    private static string? CountSuffix(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
        {
            return ".Length";
        }

        if (HasIntProperty(type, "Count"))
        {
            return ".Count";
        }

        return HasIntProperty(type, "Length") ? ".Length" : null;
    }

    private static string? GetMemberCountText(ExpressionSyntax expression)
    {
        if (expression is not MemberAccessExpressionSyntax access || !IsNameChain(access.Expression))
        {
            return null;
        }

        var name = access.Name.Identifier.ValueText;
        var isCount = string.Equals(name, "Count", StringComparison.Ordinal)
                      || string.Equals(name, "Length", StringComparison.Ordinal);
        return isCount ? expression.ToString() : null;
    }

    private static bool IsNameChain(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax => true,
            MemberAccessExpressionSyntax access => IsNameChain(access.Expression),
            _ => false
        };
    }

    private static bool HasIntProperty(ITypeSymbol type, string propertyName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (HasAccessibleIntProperty(current, propertyName))
            {
                return true;
            }
        }

        return type.AllInterfaces.Any(candidate => HasAccessibleIntProperty(candidate, propertyName));
    }

    private static bool HasAccessibleIntProperty(ITypeSymbol type, string propertyName)
    {
        foreach (var property in type.GetMembers(propertyName).OfType<IPropertySymbol>())
        {
            if (property.IsStatic
                || property.Type.SpecialType is not SpecialType.System_Int32
                || !property.ExplicitInterfaceImplementations.IsEmpty)
            {
                continue;
            }

            if (property.DeclaredAccessibility is Accessibility.Public
                or Accessibility.Internal
                or Accessibility.ProtectedOrInternal)
            {
                return true;
            }
        }

        return false;
    }
}
