using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the GeneratedRegex code fix providers for rewrites the main fix suites leave out.
/// </summary>
public sealed class GeneratedRegexCodeFixBranchCoverageTests
{
    private const string PartialImpl = "    private static partial System.Text.RegularExpressions.Regex Probe() => null!;";

    private static string Wrap(string attribute)
    {
        return $$"""
                 using System.Text.RegularExpressions;
                 partial class C
                 {
                     [{{attribute}}]
                     private static partial Regex Probe();
                 {{PartialImpl}}
                 }
                 """;
    }

    private static Task CompiledFixAsync(string before, string after)
    {
        return CodeFixTestRunner.RunAsync<GeneratedRegexAnalyzer, GeneratedRegexCompiledCodeFixProvider>(
            Wrap("{|E128012:" + before + "|}"),
            Wrap(after));
    }

    private static Task NestedFixAsync(string before, string after)
    {
        return CodeFixTestRunner.RunAsync<GeneratedRegexAnalyzer, GeneratedRegexNestedCodeFixProvider>(
            Wrap("{|E128014:" + before + "|}"),
            Wrap(after));
    }

    private static Task OverlappingFixAsync(string before, string after)
    {
        return CodeFixTestRunner.RunAsync<GeneratedRegexAnalyzer, GeneratedRegexOverlappingCodeFixProvider>(
            Wrap("{|E128013:" + before + "|}"),
            Wrap(after));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Compiled_RepeatedOnBothSides_BecomesNone()
    {
        return CompiledFixAsync(
            "GeneratedRegex(@\"\\d+\", RegexOptions.Compiled | RegexOptions.Compiled, 1000)",
            "GeneratedRegex(@\"\\d+\", RegexOptions.None, 1000)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Compiled_OnTheRight_KeepsTheLeftOption()
    {
        return CompiledFixAsync(
            "GeneratedRegex(@\"\\d+\", RegexOptions.IgnoreCase | RegexOptions.Compiled, 1000)",
            "GeneratedRegex(@\"\\d+\", RegexOptions.IgnoreCase, 1000)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Compiled_FirstOfThree_KeepsTheOtherTwo()
    {
        return CompiledFixAsync(
            "GeneratedRegex(@\"\\d+\", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline, 1000)",
            "GeneratedRegex(@\"\\d+\", RegexOptions.IgnoreCase | RegexOptions.Multiline, 1000)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Compiled_MiddleOfThree_KeepsTheOtherTwo()
    {
        return CompiledFixAsync(
            "GeneratedRegex(@\"\\d+\", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline, 1000)",
            "GeneratedRegex(@\"\\d+\", RegexOptions.IgnoreCase | RegexOptions.Multiline, 1000)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Compiled_LastOfThree_KeepsTheOtherTwo()
    {
        return CompiledFixAsync(
            "GeneratedRegex(@\"\\d+\", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, 1000)",
            "GeneratedRegex(@\"\\d+\", RegexOptions.IgnoreCase | RegexOptions.Multiline, 1000)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Compiled_InNamedOptionsArgument_BecomesNone()
    {
        return CompiledFixAsync(
            "GeneratedRegex(@\"\\d+\", options: RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)",
            "GeneratedRegex(@\"\\d+\", options: RegexOptions.None, matchTimeoutMilliseconds: 1000)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Nested_LazyInnerPlus_KeepsOuterStarAndAddsNonCapturingGroup()
    {
        return CodeFixTestRunner.RunKeepDiagnosticAsync<GeneratedRegexAnalyzer, GeneratedRegexNestedCodeFixProvider>(
            Wrap("{|E128014:GeneratedRegex(@\"(a+?)*\", RegexOptions.None, 1000)|}"),
            Wrap("{|E128014:GeneratedRegex(@\"(?:a+?)*\", RegexOptions.None, 1000)|}"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Nested_LazyInnerStar_RemovesOuterStar()
    {
        return NestedFixAsync(
            "GeneratedRegex(@\"(a*?)*\", RegexOptions.None, 1000)",
            "GeneratedRegex(@\"(a*?)\", RegexOptions.None, 1000)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Nested_RegularStringLiteral_KeepsEscapedForm()
    {
        return NestedFixAsync(
            "GeneratedRegex(\"(\\\\w+)+\", RegexOptions.None, 1000)",
            "GeneratedRegex(\"(\\\\w+)\", RegexOptions.None, 1000)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Nested_VerbatimLiteralWithQuote_DoublesTheQuote()
    {
        return NestedFixAsync(
            "GeneratedRegex(@\"(\"\"+)+\", RegexOptions.None, 1000)",
            "GeneratedRegex(@\"(\"\"+)\", RegexOptions.None, 1000)");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("(a+){2,}")]
    [InlineData("(a+a)+")]
    [InlineData("(?:a+)*")]
    public Task Nested_ShapeWithoutSafeRewrite_OffersNoFix(string pattern)
    {
        return CodeFixTestRunner.NoFixAsync<GeneratedRegexAnalyzer, GeneratedRegexNestedCodeFixProvider>(
            Wrap("{|E128014:GeneratedRegex(@\"" + pattern + "\", RegexOptions.None, 1000)|}"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Overlapping_QuantifiedDotBeforeRequiredWhitespace_KeepsOneSpace()
    {
        return OverlappingFixAsync(
            "GeneratedRegex(@\".*\\s+\", RegexOptions.None, 1000)",
            "GeneratedRegex(@\".*\\s\", RegexOptions.None, 1000)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Overlapping_QuantifiedDotBeforeOptionalWhitespace_DropsTheToken()
    {
        return OverlappingFixAsync(
            "GeneratedRegex(@\"x.*\\s*\", RegexOptions.None, 1000)",
            "GeneratedRegex(@\"x.*\", RegexOptions.None, 1000)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Overlapping_CharacterClassBeforeToken_SkipsClassContents()
    {
        return OverlappingFixAsync(
            "GeneratedRegex(@\"[\\s]\\s*.*\", RegexOptions.None, 1000)",
            "GeneratedRegex(@\"[\\s].*\", RegexOptions.None, 1000)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Overlapping_RegularStringLiteral_KeepsEscapedForm()
    {
        return OverlappingFixAsync(
            "GeneratedRegex(\"\\\\s*.*\", RegexOptions.None, 1000)",
            "GeneratedRegex(\".*\", RegexOptions.None, 1000)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Timeout_FullyQualifiedAttribute_AddsBothUsings()
    {
        return CodeFixTestRunner.RunAsync<GeneratedRegexAnalyzer, GeneratedRegexTimeoutCodeFixProvider>(
            $$"""
              partial class C
              {
                  [{|E128011:System.Text.RegularExpressions.GeneratedRegex(@"\d+")|}]
                  private static partial System.Text.RegularExpressions.Regex Probe();
              {{PartialImpl}}
              }
              """,
            $$"""
              using System.Text.RegularExpressions;
              using System.Threading;

              partial class C
              {
                  [System.Text.RegularExpressions.GeneratedRegex(@"\d+", RegexOptions.None, Timeout.Infinite)]
                  private static partial System.Text.RegularExpressions.Regex Probe();
              {{PartialImpl}}
              }
              """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Compiled_NamedOptionsAfterAnotherNamedArgument_BecomesNone()
    {
        return CompiledFixAsync(
            "GeneratedRegex(@\"\\d+\", matchTimeoutMilliseconds: 1000, options: RegexOptions.Compiled)",
            "GeneratedRegex(@\"\\d+\", matchTimeoutMilliseconds: 1000, options: RegexOptions.None)");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Compiled_NextToConstantOfAnotherType_KeepsTheOtherConstant()
    {
        return CodeFixTestRunner.RunAsync<GeneratedRegexAnalyzer, GeneratedRegexCompiledCodeFixProvider>(
            """
            using System.Text.RegularExpressions;
            static class Flags
            {
                public const RegexOptions Compiled = RegexOptions.IgnoreCase;
            }
            partial class C
            {
                [{|E128012:GeneratedRegex(@"\d+", Flags.Compiled | RegexOptions.Compiled, 1000)|}]
                private static partial Regex Probe();
                private static partial Regex Probe() => null!;
            }
            """,
            """
            using System.Text.RegularExpressions;
            static class Flags
            {
                public const RegexOptions Compiled = RegexOptions.IgnoreCase;
            }
            partial class C
            {
                [GeneratedRegex(@"\d+", Flags.Compiled, 1000)]
                private static partial Regex Probe();
                private static partial Regex Probe() => null!;
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Fake")]
    [InlineData("Fake.RegularExpressions")]
    [InlineData("Fake.Text.RegularExpressions")]
    [InlineData("Outer.System.Text.RegularExpressions")]
    public Task Compiled_NextToLookAlikeRegexOptionsType_KeepsTheLookAlike(string lookAlikeNamespace)
    {
        var declaration = $$"""
                            namespace {{lookAlikeNamespace}}
                            {
                                static class RegexOptions
                                {
                                    public const global::System.Text.RegularExpressions.RegexOptions Compiled =
                                        global::System.Text.RegularExpressions.RegexOptions.IgnoreCase;
                                }
                            }
                            """;
        return CodeFixTestRunner.RunAsync<GeneratedRegexAnalyzer, GeneratedRegexCompiledCodeFixProvider>(
            $$"""
              using System.Text.RegularExpressions;
              {{declaration}}
              partial class C
              {
                  [{|E128012:GeneratedRegex(@"\d+", {{lookAlikeNamespace}}.RegexOptions.Compiled | RegexOptions.Compiled, 1000)|}]
                  private static partial Regex Probe();
                  private static partial Regex Probe() => null!;
              }
              """,
            $$"""
              using System.Text.RegularExpressions;
              {{declaration}}
              partial class C
              {
                  [GeneratedRegex(@"\d+", {{lookAlikeNamespace}}.RegexOptions.Compiled, 1000)]
                  private static partial Regex Probe();
                  private static partial Regex Probe() => null!;
              }
              """);
    }
}
