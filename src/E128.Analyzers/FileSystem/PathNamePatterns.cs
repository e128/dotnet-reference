using System;
using System.Collections.Immutable;

namespace E128.Analyzers.FileSystem;

/// <summary>
///     One classifier for a name that suggests a file system path. Every rule that reads a name
///     for this purpose calls into this class.
/// </summary>
internal static class PathNamePatterns
{
    private static readonly ImmutableArray<string> Exclusions = ["xpath"];

    private static readonly ImmutableArray<string> Patterns = ["path", "dir", "directory"];

    internal static bool IsPathName(string name)
    {
        foreach (var exclusion in Exclusions)
        {
            if (Contains(name, exclusion))
            {
                return false;
            }
        }

        foreach (var pattern in Patterns)
        {
            if (Contains(name, pattern))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsPathCliName(string name)
    {
        return IsPathName(name)
               || Contains(name, "input")
               || Contains(name, "output")
               || Contains(name, "file");
    }

    internal static PathNameKind Classify(string name)
    {
        if (Contains(name, "dir") || Contains(name, "folder"))
        {
            return PathNameKind.Directory;
        }

        return Contains(name, "file") ? PathNameKind.File : PathNameKind.Ambiguous;
    }

    private static bool Contains(string name, string value)
    {
        return name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
