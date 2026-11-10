using System.Collections.Generic;

namespace E128.Analyzers.Security;

/// <summary>
///     Splits an identifier at the separators a caller names, plus camel-case boundaries. The FIPS
///     name rules share this one implementation so a name shape is read the same way everywhere.
/// </summary>
internal static class WordSplitter
{
    internal static List<string> Split(string name, string separators)
    {
        var words = new List<string>();
        var start = 0;

        for (var i = 1; i <= name.Length; i++)
        {
            if (!IsBoundary(name, i, separators))
            {
                continue;
            }

            var word = TrimSeparators(name.Substring(start, i - start), separators);

            if (word.Length > 0)
            {
                words.Add(word);
            }

            start = i;
        }

        return words;
    }

    private static bool IsBoundary(string name, int i, string separators)
    {
        return i == name.Length
               || separators.IndexOf(name[i]) >= 0
               || IsCaseBoundary(name, i);
    }

    private static bool IsCaseBoundary(string name, int i)
    {
        return (char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
               || (char.IsUpper(name[i - 1]) && char.IsUpper(name[i])
                   && i + 1 < name.Length && char.IsLower(name[i + 1]));
    }

    private static string TrimSeparators(string word, string separators)
    {
        var start = 0;
        var end = word.Length;

        while (start < end && separators.IndexOf(word[start]) >= 0)
        {
            start++;
        }

        while (end > start && separators.IndexOf(word[end - 1]) >= 0)
        {
            end--;
        }

        return word.Substring(start, end - start);
    }
}
