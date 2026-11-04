using System;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Style;

/// <summary>
///     Reads the <c lang="csharp">e128_tracked_ref_pattern</c> option shared by the untracked-deferral rules. The pattern matches a
///     work-item reference such as <c lang="csharp">AB#123</c> or <c lang="csharp">#123</c>.
/// </summary>
internal static class TrackerReference
{
    internal const string OptionKey = "e128_tracked_ref_pattern";

    private const string DefaultPattern = @"#\d+";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    internal static Regex GetPattern(AnalyzerConfigOptions options)
    {
        if (options.TryGetValue(OptionKey, out var configured) && !string.IsNullOrWhiteSpace(configured))
        {
            try
            {
                return new Regex(configured, RegexOptions.CultureInvariant, MatchTimeout);
            }
            catch (ArgumentException)
            {
                // An invalid pattern falls back to the default instead of failing the build.
            }
        }

        return new Regex(DefaultPattern, RegexOptions.CultureInvariant, MatchTimeout);
    }

    internal static bool IsTracked(Regex pattern, string text)
    {
        try
        {
            return pattern.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }
}
