using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SkipInitCodeFixProviderBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheCallIsNotAStatement()
    {
        return CodeFixTestRunner.NoFixAsync<SkipInitAnalyzer, SkipInitCodeFixProvider>(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    int value;
                    void Local() => {|E128120:Unsafe.SkipInit(out value)|};
                    Local();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheArgumentIsAnArrayElement()
    {
        return CodeFixTestRunner.NoFixAsync<SkipInitAnalyzer, SkipInitCodeFixProvider>(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M(int[] values)
                {
                    {|E128120:Unsafe.SkipInit(out values[0])|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheArgumentIsATypedDiscard()
    {
        return CodeFixTestRunner.NoFixAsync<SkipInitAnalyzer, SkipInitCodeFixProvider>(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    {|E128120:Unsafe.SkipInit(out int _)|};
                }
            }
            """);
    }
}
