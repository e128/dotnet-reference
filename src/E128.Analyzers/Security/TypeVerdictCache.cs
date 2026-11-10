using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers.Security;

/// <summary>
///     Holds the per-compilation verdict cache outside the analyzer type, which <c lang="csharp">RS1008</c> forbids.
/// </summary>
internal static class TypeVerdictCache
{
    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<ITypeSymbol, TypeVerdict>> Cache =
        new();

    internal static ConcurrentDictionary<ITypeSymbol, TypeVerdict> For(Compilation compilation)
    {
        return Cache.GetValue(compilation, static _ => new ConcurrentDictionary<ITypeSymbol, TypeVerdict>(SymbolEqualityComparer.Default));
    }
}
