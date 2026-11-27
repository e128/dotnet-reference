using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GeneratedRegexSecurityTests
{
    private const string PartialImpl = "    private static partial System.Text.RegularExpressions.Regex DigitsOnly() => null!;";

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAlternativesShareAFirstAtom()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [{|E128264:GeneratedRegex(@"^(a|ab)+$", RegexOptions.None, 1000)|}]
                                 private static partial Regex DigitsOnly();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAlternativesAreExclusive()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [GeneratedRegex(@"^(a|b)+$", RegexOptions.None, 1000)]
                                 private static partial Regex DigitsOnly();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGroupIsAtomic()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [GeneratedRegex(@"(?>a|ab)+", RegexOptions.None, 1000)]
                                 private static partial Regex DigitsOnly();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePatternHasThreeWildcards()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [{|E128013:GeneratedRegex(@".*error.*timeout.*", RegexOptions.None, 1000)|}]
                                 private static partial Regex DigitsOnly();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePatternHasTwoWildcards()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [GeneratedRegex(@"^.*error$", RegexOptions.None, 1000)]
                                 private static partial Regex DigitsOnly();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheRepeatedGroupEndsInAnOptionalAtom()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [{|E128014:GeneratedRegex(@"(\w+[-.]?)+", RegexOptions.None, 1000)|}]
                                 private static partial Regex DigitsOnly();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheOptionalAtomHasNoInnerQuantifier()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [GeneratedRegex(@"(ab?)+", RegexOptions.None, 1000)]
                                 private static partial Regex DigitsOnly();
                                 {{PartialImpl}}
                             }
                             """);
    }
}
