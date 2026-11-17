using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GeneratedRegexCompiledParenthesizedTests
{
    private const string PartialImpl = "    private static partial System.Text.RegularExpressions.Regex DigitsOnly() => null!;";

    /// <summary>
    ///     Parentheses do not change the options value, so a parenthesized Compiled flag is as
    ///     redundant as a bare one. The fix drops the flag and the parentheses it leaves redundant.
    /// </summary>
    [Theory]
    [Trait("Category", "CI")]
    [InlineData("RegexOptions.IgnoreCase | (RegexOptions.Multiline | RegexOptions.Compiled)", "RegexOptions.IgnoreCase | RegexOptions.Multiline")]
    [InlineData("(RegexOptions.Compiled | RegexOptions.IgnoreCase) | RegexOptions.Multiline", "RegexOptions.IgnoreCase | RegexOptions.Multiline")]
    [InlineData("(RegexOptions.Compiled)", "RegexOptions.None")]
    [InlineData("RegexOptions.IgnoreCase | (RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled)", "RegexOptions.IgnoreCase | (RegexOptions.Multiline | RegexOptions.Singleline)")]
    public Task ParenthesizedCompiled_IsReportedAndRemoved(string options, string expected)
    {
        return CodeFixTestRunner.RunAsync<GeneratedRegexAnalyzer, GeneratedRegexCompiledCodeFixProvider>(
            $$"""
              using System.Text.RegularExpressions;
              using System.Threading;
              partial class C
              {
                  [{|E128012:GeneratedRegex(@"\d+", {{options}}, 1000)|}]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """,
            $$"""
              using System.Text.RegularExpressions;
              using System.Threading;
              partial class C
              {
                  [GeneratedRegex(@"\d+", {{expected}}, 1000)]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """);
    }

    /// <summary>
    ///     Parenthesized options without the Compiled flag stay silent.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task ParenthesizedOptionsWithoutCompiled_Silent()
    {
        return CodeFixTestRunner.NoFixAsync<GeneratedRegexAnalyzer, GeneratedRegexCompiledCodeFixProvider>(
            $$"""
              using System.Text.RegularExpressions;
              using System.Threading;
              partial class C
              {
                  [GeneratedRegex(@"\d+", RegexOptions.IgnoreCase | (RegexOptions.Multiline | RegexOptions.Singleline), 1000)]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """);
    }
}
