using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers;

/// <summary>
///     Recognition of <c lang="csharp">Task.WhenAll</c> calls shared by the WhenAll reliability rules.
/// </summary>
internal static class TaskWhenAllCalls
{
    /// <summary>
    ///     True when the invocation calls <c lang="csharp">Task.WhenAll</c> on <paramref name="taskType" />.
    /// </summary>
    internal static bool IsWhenAll(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol taskType)
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };

        if (!string.Equals(methodName, "WhenAll", StringComparison.Ordinal))
        {
            return false;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        return symbol is IMethodSymbol method
               && SymbolEqualityComparer.Default.Equals(method.ContainingType, taskType);
    }
}
