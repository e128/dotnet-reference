using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StringFormatToInterpolationCodeFixProviderBranchCoverageGapTests
{
    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<StringFormatToInterpolationAnalyzer, StringFormatToInterpolationCodeFixProvider>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAClosingBraceIsFollowedByText()
    {
        return VerifyNoFixAsync(
            """
            class C
            {
                void M(int n)
                {
                    var s = {|E128015:string.Format("} {0}", n)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAnOpeningBraceEndsTheFormat()
    {
        return VerifyNoFixAsync(
            """
            class C
            {
                void M(int n)
                {
                    var s = {|E128015:string.Format("{0} {", n)|};
                }
            }
            """);
    }
}
