using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Unsafe;

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
}
