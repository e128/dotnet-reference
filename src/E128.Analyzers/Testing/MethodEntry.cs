using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers.Testing;

/// <summary>
///   One candidate test method, scored against the pure detectors and carrying the duplicate-coverage keys
///   the compilation-end pass groups on.
/// </summary>
internal sealed class MethodEntry
{
    internal MethodEntry(
        Location location,
        string methodName,
        ImmutableArray<string> conditions,
        ImmutableArray<string> duplicateKeys)
    {
        Location = location;
        MethodName = methodName;
        Conditions = conditions;
        DuplicateKeys = duplicateKeys;
    }

    internal Location Location { get; }

    internal string MethodName { get; }

    /// <summary>
    ///   Names of the pure detectors this method matched, in detector order. The compilation-end pass appends
    ///   DuplicateCoverage when a key repeats, so the set is complete only there.
    /// </summary>
    internal ImmutableArray<string> Conditions { get; }

    internal ImmutableArray<string> DuplicateKeys { get; }
}
