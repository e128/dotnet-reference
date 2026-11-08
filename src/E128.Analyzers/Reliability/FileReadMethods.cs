using System;
using System.Collections.Immutable;

namespace E128.Analyzers.Reliability;

/// <summary>
///     The <c lang="csharp">System.IO.File</c> members that read a file. The TOCTOU and
///     process-output rules share this set, so both report the same read shapes.
/// </summary>
internal static class FileReadMethods
{
    private static readonly ImmutableHashSet<string> Names = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ReadAllBytes",
        "ReadAllBytesAsync",
        "ReadAllText",
        "ReadAllTextAsync",
        "ReadAllLines",
        "ReadAllLinesAsync",
        "OpenRead",
        "Open",
        "OpenText");

    internal static bool Contains(string methodName)
    {
        return Names.Contains(methodName);
    }
}
