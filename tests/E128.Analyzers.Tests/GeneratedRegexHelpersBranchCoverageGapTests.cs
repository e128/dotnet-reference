using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GeneratedRegexHelpersBranchCoverageGapTests
{
    private static Task VerifyNestedAsync(string pattern)
    {
        var source = "using System.Text.RegularExpressions;\n"
                     + "partial class C\n"
                     + "{\n"
                     + "    [{|E128014:GeneratedRegex(@\"" + pattern + "\", RegexOptions.None, 1000)|}]\n"
                     + "    private static partial Regex Probe();\n"
                     + "    private static partial Regex Probe() => null!;\n"
                     + "}\n";
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedQuantifier_Fires_WhenAnEscapedBracketPairHidesTheClassEnd()
    {
        return VerifyNestedAsync(@"(b+\[a\])+");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData(@"\s", 0, -1)]
    [InlineData(@"\sa", 0, -1)]
    [InlineData(@"\s+", 0, 3)]
    [InlineData(@"\s*?", 0, 4)]
    [InlineData(@"a\s+", 1, 4)]
    public void WhitespaceQuantifierEnd_ReturnsTheIndexAfterTheQuantifier(string pattern, int index, int expected)
    {
        Assert.Equal(expected, GeneratedRegexHelpers.WhitespaceQuantifierEnd(pattern, index));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void FindNestedQuantifierSpan_ReturnsEqualSpansForTheSamePattern()
    {
        var found = GeneratedRegexHelpers.FindNestedQuantifierSpan("(a+)+");
        var foundAgain = GeneratedRegexHelpers.FindNestedQuantifierSpan("(a+)+");

        var first = Assert.NotNull(found);
        var second = Assert.NotNull(foundAgain);
        Assert.Equal(new GeneratedRegexHelpers.NestedQuantifierSpan(0, 3, 4, 1, true), first);
        Assert.True(first.Equals(second));
        Assert.True(first.Equals((object)second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData(9, 3, 4, 1, true)]
    [InlineData(0, 9, 4, 1, true)]
    [InlineData(0, 3, 9, 1, true)]
    [InlineData(0, 3, 4, 9, true)]
    [InlineData(0, 3, 4, 1, false)]
    public void Equals_ReturnsFalse_WhenOneFieldDiffers(int open, int close, int quantifier, int length, bool singleAtom)
    {
        var baseline = new GeneratedRegexHelpers.NestedQuantifierSpan(0, 3, 4, 1, true);
        var other = new GeneratedRegexHelpers.NestedQuantifierSpan(open, close, quantifier, length, singleAtom);

        Assert.False(baseline.Equals(other));
        Assert.False(baseline.Equals((object)other));
        Assert.NotEqual(baseline.GetHashCode(), other.GetHashCode());
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Equals_ReturnsFalse_ForAValueOfAnotherType()
    {
        var span = new GeneratedRegexHelpers.NestedQuantifierSpan(0, 3, 4, 1, true);

        Assert.False(span.Equals("text"));
        Assert.False(span.Equals(null));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ToString_ListsEveryField()
    {
        var span = new GeneratedRegexHelpers.NestedQuantifierSpan(1, 2, 3, 4, true);

        Assert.Equal(
            "NestedQuantifierSpan(open: 1, close: 2, quantifier: 3, length: 4, singleAtomBody: True)",
            span.ToString());
    }
}
