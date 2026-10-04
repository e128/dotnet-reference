using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers.Testing;

/// <summary>
///     One candidate test method, scored against the pure detectors and carrying the duplicate-coverage keys
///     the compilation-end pass groups on.
/// </summary>
internal sealed class MethodEntry
{
    internal MethodEntry(
        Location location,
        string methodName,
        int hitCount,
        ImmutableArray<string> duplicateKeys)
    {
        Location = location;
        MethodName = methodName;
        HitCount = hitCount;
        DuplicateKeys = duplicateKeys;
    }

    internal Location Location { get; }

    internal string MethodName { get; }

    internal int HitCount { get; }

    internal ImmutableArray<string> DuplicateKeys { get; }
}
