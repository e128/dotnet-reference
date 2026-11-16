using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Drives the shared regex pattern scanner through the GeneratedRegex analyzer. Each row
///     states whether a pattern reports E128014 (nested quantifier) and E128013 (overlapping
///     quantifier).
/// </summary>
public sealed class GeneratedRegexHelpersBranchCoverageTests
{
    private static Task VerifyPatternAsync(string pattern, bool nested, bool overlapping)
    {
        var marked = $"GeneratedRegex(@\"{pattern}\", RegexOptions.None, 1000)";
        if (overlapping)
        {
            marked = "{|E128013:" + marked + "|}";
        }

        if (nested)
        {
            marked = "{|E128014:" + marked + "|}";
        }

        var source = "using System.Text.RegularExpressions;\n"
                     + "partial class C\n"
                     + "{\n"
                     + "    [" + marked + "]\n"
                     + "    private static partial Regex Probe();\n"
                     + "    private static partial Regex Probe() => null!;\n"
                     + "}\n";
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>(source);
    }

    /// <summary>
    ///     Single-atom and multi-atom bodies that repeat an inner quantifier.
    /// </summary>
    [Theory]
    [Trait("Category", "CI")]
    [InlineData("(a+)+")]
    [InlineData("(a+)*")]
    [InlineData("(a+)+?")]
    [InlineData("(a+)*?")]
    [InlineData("(a+){2,}")]
    [InlineData("(a+){2,}?")]
    [InlineData(@"(\w+)+")]
    [InlineData(@"(?:\d+)+")]
    [InlineData("(?:a+?)+")]
    [InlineData("([ab]+)+")]
    [InlineData("(?:a|b+)+")]
    [InlineData("(?<name>a+)+")]
    [InlineData("(?=a+)+")]
    [InlineData("(?+)+")]
    [InlineData("((a)+b)+")]
    [InlineData("(a+a)+")]
    [InlineData(@"(a+\w)+")]
    [InlineData(@"(\w+\d)+")]
    [InlineData(@"(\d+\w)+")]
    [InlineData(@"(\d+5)+")]
    [InlineData(@"(\t+x)+")]
    [InlineData("(.+x)+")]
    [InlineData("(x+.)+")]
    [InlineData("(a+[ab])+")]
    [InlineData("(a+[a-c])+")]
    [InlineData("(a+[^b])+")]
    [InlineData(@"(\A+x)+")]
    [InlineData(@"(\B+x)+")]
    [InlineData(@"(\Z+x)+")]
    [InlineData(@"(\x+x)+")]
    [InlineData(@"(\z+x)+")]
    [InlineData(@"(\p+x)+")]
    [InlineData(@"(a+[\S])+")]
    [InlineData(@"(-+[\W])+")]
    [InlineData(@"(5+[\d\w])+")]
    [InlineData(@"(a+[\D])+")]
    [InlineData("(+)+")]
    [InlineData(@"(\r+[\r])+")]
    [InlineData(@"(\t+[\t])+")]
    [InlineData(@"(\f+[\f])+")]
    [InlineData(@"(\v+[\v])+")]
    [InlineData(@"(\b+[\b])+")]
    [InlineData(@"(\0+[\0])+")]
    [InlineData(@"(\W+-)+")]
    [InlineData(@"(\D+a)+")]
    [InlineData(@"(\S+a)+")]
    [InlineData(@"(\w+a)+")]
    [InlineData(@"(\s+ )+")]
    [InlineData("(a+)+[")]
    [InlineData("(a+)+[^")]
    [InlineData("(a+)+[]")]
    [InlineData(@"(\d+\.)+")]
    [InlineData(@"\\(a+)+")]
    [InlineData(@"(5+[\d])+")]
    [InlineData(@"( +[\s])+")]
    [InlineData("([ab]+a)+")]
    [InlineData(@"(a+[\w])+")]
    [InlineData(@"(\n+[\n])+")]
    [InlineData("(a+[c-a])+")]
    [InlineData("(a+[]a])+")]
    [InlineData("(a+[^]b])+")]
    [InlineData("(a+[a-])+")]
    [InlineData(@"(a+[\x-z])+")]
    [InlineData(@"(a+[\-a])+")]
    [InlineData("(a+x])+")]
    [InlineData("(a+2})+")]
    [InlineData("(a+a{2})+")]
    [InlineData("(a+a{2,3}?)+")]
    [InlineData(@"(a+\w[ab]+)+")]
    [InlineData(@"(a+\d[a-c]*)+")]
    [InlineData("([a]+[a]+)+")]
    [InlineData("(.+.+)+")]
    public Task NestedQuantifier_Fires(string pattern)
    {
        return VerifyPatternAsync(pattern, true, false);
    }

    /// <summary>
    ///     Shapes the scanner must leave alone.
    /// </summary>
    [Theory]
    [Trait("Category", "CI")]
    [InlineData("(?>a+)+")]
    [InlineData("(?>a+")]
    [InlineData("(a+")]
    [InlineData("(a+){2}")]
    [InlineData("(a+){2,3}")]
    [InlineData("(a+){x,}")]
    [InlineData("(a+){2")]
    [InlineData("(a+){2,")]
    [InlineData("(a+){")]
    [InlineData("(a+)")]
    [InlineData("(a+)?")]
    [InlineData("(ab)+")]
    [InlineData("(ab+)")]
    [InlineData("(a+b+)+")]
    [InlineData("(a+[^a])+")]
    [InlineData("(a+[b-d])+")]
    [InlineData(@"(\~+x)+")]
    [InlineData(@"(\_+x)+")]
    [InlineData(@"(\@+x)+")]
    [InlineData(@"(\1+x)+")]
    [InlineData(@"(z+[a-\d])+")]
    [InlineData(@"(a+[\W])+")]
    [InlineData(@"(a+[\s])+")]
    [InlineData(@"(a+[\d])+")]
    [InlineData(@"(a+[\n])+")]
    [InlineData("(a+[+-])+")]
    [InlineData("(a+[A])+")]
    [InlineData(@"(a+[\])+")]
    [InlineData(@"(a+[\.])+")]
    [InlineData(@"(a+\.)+")]
    [InlineData("(/+[^/])+")]
    [InlineData(@"(\d+x+)+")]
    [InlineData(@"(\w+-)+")]
    [InlineData(@"(\s+x)+")]
    [InlineData(@"(\D+5)+")]
    [InlineData(@"(\W+a)+")]
    [InlineData(@"(\S+ )+")]
    [InlineData("(a+b{2})+")]
    [InlineData("(a+b{2,})+")]
    [InlineData("(?:/[^/]+)+")]
    [InlineData(@"\(a+\)+")]
    [InlineData("[(a+)]+")]
    [InlineData("[(]a+[)]+")]
    [InlineData("[ab")]
    public Task NestedQuantifier_Silent(string pattern)
    {
        return VerifyPatternAsync(pattern, false, false);
    }

    /// <summary>
    ///     Overlapping quantifiers that report E128013.
    /// </summary>
    [Theory]
    [Trait("Category", "CI")]
    [InlineData(@"\s*.*")]
    [InlineData(@"\s+.+")]
    [InlineData(@"\s*.?")]
    [InlineData(@"\s*?.*")]
    [InlineData(@"\s+?(.*)")]
    [InlineData(@"\s*(a.*)")]
    [InlineData(@"\s*((a)b.*)")]
    [InlineData(@"\s*[^a]+")]
    [InlineData(@"\s*[^a]*")]
    [InlineData(@"\s*[^\d]+")]
    [InlineData(@"\s*[^a\\]+")]
    [InlineData(@".*\s+")]
    [InlineData(@".+?\s+")]
    [InlineData(@"\s*(\w.*)")]
    [InlineData(@"\s*([x].*)")]
    [InlineData(@"([x].*)\s+")]
    [InlineData(@"(\w.*)\s+")]
    [InlineData(@"(.?)*\s+")]
    [InlineData(@"(.*)\s+")]
    [InlineData(@"(a.*)\s*")]
    [InlineData(@"a.*\s*b")]
    public Task OverlappingQuantifier_Fires(string pattern)
    {
        return VerifyPatternAsync(pattern, false, true);
    }

    /// <summary>
    ///     Whitespace quantifiers with no overlapping neighbor.
    /// </summary>
    [Theory]
    [Trait("Category", "CI")]
    [InlineData(@"\s*a")]
    [InlineData(@"\s+")]
    [InlineData(@"\s")]
    [InlineData(@"\s?.")]
    [InlineData(@"\s*.")]
    [InlineData(@"\s*")]
    [InlineData(@"\s*(a)")]
    [InlineData(@"((a.*)b)\s+")]
    [InlineData(@"\s*()")]
    [InlineData(@"\s*(")]
    [InlineData(@"\s*[^a]")]
    [InlineData(@"\s*[^a")]
    [InlineData(@"\s*[^\s]+")]
    [InlineData(@"\s*[^a\s]+")]
    [InlineData(@"\s*[a]+")]
    [InlineData(@"\s*[^]")]
    [InlineData(@"?\s+")]
    [InlineData(@"*\s+")]
    [InlineData(@"a?\s+")]
    [InlineData(@"a*\s+")]
    [InlineData(@"a+?\s+")]
    [InlineData(@"(a)\s+")]
    [InlineData(@"(a)*\s+")]
    [InlineData(@"\)\s+")]
    [InlineData(@"[.*]\s+")]
    [InlineData(@"(a)b\s+")]
    [InlineData(@"\.\*\s+")]
    [InlineData(@"\s+[.*]")]
    [InlineData(@"^\s+$")]
    public Task WhitespaceQuantifier_Silent(string pattern)
    {
        return VerifyPatternAsync(pattern, false, false);
    }

    /// <summary>
    ///     Nested and overlapping at once.
    /// </summary>
    [Theory]
    [Trait("Category", "CI")]
    [InlineData(@"(.+)+\s*")]
    [InlineData(@"\s*(.+)+")]
    [InlineData(@"(.*)*\s+")]
    public Task NestedAndOverlapping_FireTogether(string pattern)
    {
        return VerifyPatternAsync(pattern, true, true);
    }
}
