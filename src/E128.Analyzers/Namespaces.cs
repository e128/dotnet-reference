using Microsoft.CodeAnalysis;

namespace E128.Analyzers;

/// <summary>
///     Namespace-chain queries shared by the type-matching rules.
/// </summary>
internal static class Namespaces
{
    /// <summary>
    ///     Returns true when the namespace chain equals <paramref name="dotted" />, for example
    ///     <c lang="csharp">System.Net.Http</c>. The walk ends at the global namespace.
    /// </summary>
    internal static bool Equals(INamespaceSymbol? ns, string dotted)
    {
        var end = dotted.Length;
        for (; ns is not null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
        {
            if (end < 0)
            {
                return false;
            }

            var dot = end > 0 ? dotted.LastIndexOf('.', end - 1) : -1;
            var start = dot + 1;
            var length = end - start;
            if (ns.Name.Length != length || string.CompareOrdinal(ns.Name, 0, dotted, start, length) != 0)
            {
                return false;
            }

            end = dot;
        }

        return end < 0;
    }
}
