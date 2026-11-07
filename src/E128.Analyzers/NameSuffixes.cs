using System;
using System.Collections.Immutable;

namespace E128.Analyzers;

/// <summary>
///     Pascal-cased suffix matching shared by the naming heuristics.
/// </summary>
internal static class NameSuffixes
{
    /// <summary>
    ///     True when the name ends with one of the suffixes at an uppercase-letter position. The
    ///     uppercase gate rejects a bare lowercase word such as "min".
    /// </summary>
    internal static bool Matches(string name, ImmutableArray<string> suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (name.Length < suffix.Length)
            {
                continue;
            }

            var startIndex = name.Length - suffix.Length;
            if (!char.IsUpper(name[startIndex]))
            {
                continue;
            }

            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
