using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers;

/// <summary>
///     Reads the <c lang="csharp">e128_tracked_ref_pattern</c> option shared by the untracked-deferral rules. The pattern matches a
///     work-item reference such as <c lang="csharp">AB#123</c> or <c lang="csharp">#123</c>.
/// </summary>
internal static class TrackerReference
{
    internal const string OptionKey = "e128_tracked_ref_pattern";

    private const string DefaultPattern = @"#\d+";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex DefaultRegex = new(DefaultPattern, RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly ConcurrentDictionary<string, Regex> Cache = new(StringComparer.Ordinal);

    internal static Regex GetPattern(AnalyzerConfigOptions options)
    {
        return options.TryGetValue(OptionKey, out var configured) && !string.IsNullOrWhiteSpace(configured)
            ? Cache.GetOrAdd(configured, Compile)
            : DefaultRegex;
    }

    private static Regex Compile(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return DefaultRegex;
        }
    }

    internal static bool IsTracked(Regex pattern, string text)
    {
        try
        {
            return pattern.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
