using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

/// <summary>
///     Reads a <c lang="csharp">Dispose</c> or <c lang="csharp">DisposeAsync</c> call, including the
///     conditional form <c lang="csharp">field?.Dispose()</c>.
/// </summary>
internal static class DisposeCalls
{
    /// <summary>
    ///     Returns the receiver of a dispose call, or null when the invocation is not one.
    /// </summary>
    internal static ExpressionSyntax? GetReceiver(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax access
            && IsDisposeName(access.Name.Identifier.ValueText))
        {
            return access.Expression;
        }

        // A conditional call such as field?.Dispose() holds the receiver on the conditional access.
        return invocation.Expression is MemberBindingExpressionSyntax binding
               && invocation.Parent is ConditionalAccessExpressionSyntax conditional
               && IsDisposeName(binding.Name.Identifier.ValueText)
            ? conditional.Expression
            : null;
    }

    private static bool IsDisposeName(string name)
    {
        return string.Equals(name, "Dispose", StringComparison.Ordinal)
               || string.Equals(name, "DisposeAsync", StringComparison.Ordinal);
    }
}
