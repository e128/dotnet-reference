using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GeneratedRegexNestedE128CodeFixTests
{
    private const string PartialImpl = "    private static partial System.Text.RegularExpressions.Regex DigitsOnly() => null!;";

    private static Task VerifyFixAsync(string source, string fixedCode, bool keepDiagnostic = false)
    {
        return keepDiagnostic
            ? CodeFixTestRunner.RunKeepDiagnosticAsync<GeneratedRegexAnalyzer, GeneratedRegexNestedCodeFixProvider>(source, fixedCode)
            : CodeFixTestRunner.RunAsync<GeneratedRegexAnalyzer, GeneratedRegexNestedCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedQuantifier_DotPlusPlus_OuterQuantifierRemoved()
    {
        return VerifyFixAsync(
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [{|E128014:GeneratedRegex(@"(.+)+", RegexOptions.None, 1000)|}]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """,
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [GeneratedRegex(@"(.+)", RegexOptions.None, 1000)]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedQuantifier_GroupPlusStar_OuterQuantifierRemoved()
    {
        return VerifyFixAsync(
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [{|E128014:GeneratedRegex(@"(a*)*", RegexOptions.None, 1000)|}]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """,
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [GeneratedRegex(@"(a*)", RegexOptions.None, 1000)]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedQuantifier_PlusInsideStarGroup_KeepsQuantifierAsNonCapturingGroup()
    {
        return VerifyFixAsync(
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [{|E128014:GeneratedRegex(@"(a+)*", RegexOptions.None, 1000)|}]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """,
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [{|E128014:GeneratedRegex(@"(?:a+)*", RegexOptions.None, 1000)|}]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """,
            keepDiagnostic: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedQuantifier_QuotedLiteral_KeepsQuotes()
    {
        return VerifyFixAsync(
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [{|E128014:GeneratedRegex("(.+)+", RegexOptions.None, 1000)|}]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """,
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [GeneratedRegex("(.+)", RegexOptions.None, 1000)]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedQuantifier_MultiAtomBody_ReplacesOuterQuantifierWithUnboundedBrace()
    {
        return VerifyFixAsync(
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [{|E128014:GeneratedRegex(@"(?:\w\w+)+", RegexOptions.None, 1000)|}]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """,
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [{|E128014:GeneratedRegex(@"(?:\w\w+){1,}", RegexOptions.None, 1000)|}]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """,
            keepDiagnostic: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedQuantifier_UnboundedBraceOuterQuantifier_NoFixOffered()
    {
        return CodeFixTestRunner.NoFixAsync<GeneratedRegexAnalyzer, GeneratedRegexNestedCodeFixProvider>(
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [{|E128014:GeneratedRegex(@"(?:x+){3,}", RegexOptions.None, 1000)|}]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedQuantifier_QuotedLiteralWithBackslash_KeepsEscape()
    {
        return VerifyFixAsync(
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [{|E128014:GeneratedRegex("(\\w+)+", RegexOptions.None, 1000)|}]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """,
            $$"""
              using System.Text.RegularExpressions;
              partial class C
              {
                  [GeneratedRegex("(\\w+)", RegexOptions.None, 1000)]
                  private static partial Regex DigitsOnly();
                  {{PartialImpl}}
              }
              """);
    }
}
