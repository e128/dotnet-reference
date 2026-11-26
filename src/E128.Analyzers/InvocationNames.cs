using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class InvocationNames
{
    /// <summary>
    ///     Returns the called method name for both `Type.Method(...)` and a bare `Method(...)`
    ///     that a `using static` directive made available.
    /// </summary>
    internal static SimpleNameSyntax? GetSimpleName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name,
            SimpleNameSyntax name => name,
            _ => null,
        };
    }

    /// <summary>
    ///     Returns the called method name for both `Type.Method(...)` and a bare `Method(...)`.
    ///     A generic invocation such as `Method&lt;int&gt;(...)` returns null.
    /// </summary>
    internal static string? GetName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };
    }
}
