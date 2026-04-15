using System;
using System.Collections.Immutable;

namespace E128.Analyzers.FileSystem;

internal static class PathNamePatterns
{
    // Exclusions are checked first; a match here suppresses all positive patterns.
    private static readonly ImmutableArray<string> Exclusions = ["xpath"];

    // Positive patterns (case-insensitive substring match).
    // "file" is intentionally absent: filePath fires via "path", but fileName should not fire.
    private static readonly ImmutableArray<string> Patterns = ["path", "dir", "directory"];

    internal static bool IsPathName(string name)
    {
        foreach (var exclusion in Exclusions)
        {
            if (name.IndexOf(exclusion, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
        }

        foreach (var pattern in Patterns)
        {
            if (name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
