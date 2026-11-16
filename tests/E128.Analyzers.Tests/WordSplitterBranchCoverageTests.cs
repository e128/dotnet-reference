using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class WordSplitterBranchCoverageTests
{
    [Theory]
    [Trait("Category", "CI")]
    [InlineData("computedTag", "computed|Tag")]
    [InlineData("HMACKey", "HMAC|Key")]
    [InlineData("my_tag_value", "my|tag|value")]
    [InlineData("_leading_and_trailing_", "leading|and|trailing")]
    [InlineData("a.b..c", "a|b|c")]
    [InlineData("ALLCAPS", "ALLCAPS")]
    [InlineData("lower", "lower")]
    [InlineData("", "")]
    [InlineData("___", "")]
    [InlineData("X", "X")]
    public void Split_BreaksAtSeparatorsAndCaseChanges(string name, string expected)
    {
        var words = WordSplitter.Split(name, "_.");

        Assert.Equal(expected, string.Join('|', words));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Split_UsesOnlyTheSeparatorsTheCallerNames()
    {
        var words = WordSplitter.Split("a-b_c", "-");

        Assert.Equal(["a", "b_c"], words);
    }
}
