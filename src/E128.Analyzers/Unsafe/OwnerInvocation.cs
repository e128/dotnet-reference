using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

/// <summary>
///     Matches a call on an owner type shared by the Unsafe and MemoryMarshal rules.
/// </summary>
internal static class OwnerInvocation
{
    /// <summary>
    ///     True when the invocation names <paramref name="methodName" /> and its bound containing type
    ///     is one of <paramref name="ownerTypes" />. A bare call that a `using static` directive made
    ///     available counts.
    /// </summary>
    internal static bool TryMatch(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> ownerTypes,
        string methodName,
        [NotNullWhen(true)] out IMethodSymbol? method)
    {
        method = null;

        if (context.Node is not InvocationExpressionSyntax invocation
            || InvocationNames.GetSimpleName(invocation) is not { } name
            || !string.Equals(name.Identifier.ValueText, methodName, StringComparison.Ordinal))
        {
            return false;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol bound
            || !MetadataTypes.Contains(ownerTypes, bound.ContainingType))
        {
            return false;
        }

        method = bound;
        return true;
    }
}
