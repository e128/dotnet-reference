using System.Globalization;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers;

/// <summary>
///     Reads a bounded integer option from one analyzer configuration. A missing, malformed, or
///     below-minimum value yields the fallback.
/// </summary>
internal static class OptionReader
{
    internal static int ReadInt(
        AnalyzerConfigOptions options,
        string key,
        int fallback,
        int minimum)
    {
        return options.TryGetValue(key, out var text)
               && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
               && value >= minimum
            ? value
            : fallback;
    }
}
