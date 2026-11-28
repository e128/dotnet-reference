using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class InvocationNames
{
    /// <summary>
    ///     Returns the called method name for both `Type.Method(...)` and a bare `Method(...)`.
    ///     A generic invocation such as `Method&lt;int&gt;(...)` returns the simple name.
    /// </summary>
    internal static string? GetName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            SimpleNameSyntax name => name.Identifier.ValueText,
            _ => null,
        };
    }
}
