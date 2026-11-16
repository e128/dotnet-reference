using System.Threading.Tasks;
using E128.Analyzers.Style;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MidNameUnderscoreCodeFixProviderBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheNameHasOnlyATrailingUnderscore()
    {
        return CodeFixTestRunner.NoFixAsync<MidNameUnderscoreAnalyzer, MidNameUnderscoreCodeFixProvider>(
            """
            class C
            {
                private static int {|E128063:Value_|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ComputeFixedName_ReturnsAnEmptyNameUnchanged()
    {
        Assert.Equal(string.Empty, MidNameUnderscoreCodeFixProvider.ComputeFixedName(string.Empty));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("m_foo_bar", "m_fooBar")]
    [InlineData("t_foo_bar", "t_fooBar")]
    [InlineData("_foo_bar", "_fooBar")]
    [InlineData("x_foo", "xFoo")]
    [InlineData("Plain", "Plain")]
    public void ComputeFixedName_KeepsAHungarianPrefixAndMergesTheRest(string name, string expected)
    {
        Assert.Equal(expected, MidNameUnderscoreCodeFixProvider.ComputeFixedName(name));
    }
}
