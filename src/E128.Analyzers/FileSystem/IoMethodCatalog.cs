using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace E128.Analyzers.FileSystem;

internal static class IoMethodCatalog
{
    // Key: (class name, method name) → (first path-argument index, suggested type).
    private static readonly ImmutableDictionary<string, Dictionary<string, (int ArgIndex, SuggestedType Suggestion)>> Methods =
        new Dictionary<string, Dictionary<string, (int ArgIndex, SuggestedType Suggestion)>>(StringComparer.Ordinal)
        {
            ["File"] = new(StringComparer.Ordinal)
            {
                ["ReadAllText"] = (0, SuggestedType.FileInfo),
                ["ReadAllTextAsync"] = (0, SuggestedType.FileInfo),
                ["ReadAllBytes"] = (0, SuggestedType.FileInfo),
                ["ReadAllBytesAsync"] = (0, SuggestedType.FileInfo),
                ["WriteAllBytes"] = (0, SuggestedType.FileInfo),
                ["WriteAllBytesAsync"] = (0, SuggestedType.FileInfo),
                ["WriteAllText"] = (0, SuggestedType.FileInfo),
                ["WriteAllTextAsync"] = (0, SuggestedType.FileInfo),
                ["AppendAllText"] = (0, SuggestedType.FileInfo),
                ["AppendAllTextAsync"] = (0, SuggestedType.FileInfo),
                ["Create"] = (0, SuggestedType.FileInfo),
                ["Open"] = (0, SuggestedType.FileInfo),
                ["OpenRead"] = (0, SuggestedType.FileInfo),
                ["OpenWrite"] = (0, SuggestedType.FileInfo),
                ["OpenText"] = (0, SuggestedType.FileInfo),
                ["Exists"] = (0, SuggestedType.FileInfo),
                ["Delete"] = (0, SuggestedType.FileInfo),
                ["Copy"] = (0, SuggestedType.FileInfo),
                ["Move"] = (0, SuggestedType.FileInfo),
            },
            ["Directory"] = new(StringComparer.Ordinal)
            {
                ["GetFiles"] = (0, SuggestedType.DirectoryInfo),
                ["GetDirectories"] = (0, SuggestedType.DirectoryInfo),
                ["GetFileSystemEntries"] = (0, SuggestedType.DirectoryInfo),
                ["EnumerateFiles"] = (0, SuggestedType.DirectoryInfo),
                ["EnumerateDirectories"] = (0, SuggestedType.DirectoryInfo),
                ["CreateDirectory"] = (0, SuggestedType.DirectoryInfo),
                ["Delete"] = (0, SuggestedType.DirectoryInfo),
                ["Exists"] = (0, SuggestedType.DirectoryInfo),
                ["Move"] = (0, SuggestedType.DirectoryInfo),
            },
        }.ToImmutableDictionary(StringComparer.Ordinal);

    // Path methods are one-hop intermediaries: a local assigned from Path.*(param) inherits
    // path-ness and fires when subsequently passed to File.* or Directory.*.
    private static readonly ImmutableHashSet<string> PathMethodNames =
        ImmutableHashSet.Create(StringComparer.Ordinal, "Combine", "GetDirectoryName", "GetFileName", "GetFullPath");

    // Constructors that accept a path string as their first argument.
    private static readonly ImmutableDictionary<string, SuggestedType> ConstructorTypes =
        new Dictionary<string, SuggestedType>(StringComparer.Ordinal)
        {
            ["FileInfo"] = SuggestedType.FileInfo,
            ["DirectoryInfo"] = SuggestedType.DirectoryInfo,
            ["StreamReader"] = SuggestedType.FileInfo,
            ["StreamWriter"] = SuggestedType.FileInfo,
            ["FileStream"] = SuggestedType.FileInfo,
        }.ToImmutableDictionary(StringComparer.Ordinal);

    internal static bool TryGetMethodInfo(string className, string methodName,
        out (int ArgIndex, SuggestedType Suggestion) info)
    {
        if (Methods.TryGetValue(className, out var classMethods)
            && classMethods.TryGetValue(methodName, out info))
        {
            return true;
        }

        info = default;
        return false;
    }

    internal static bool IsPathMethod(string methodName) =>
        PathMethodNames.Contains(methodName);

    internal static bool TryGetConstructorInfo(string typeName, out SuggestedType suggestion) =>
        ConstructorTypes.TryGetValue(typeName, out suggestion);
}
