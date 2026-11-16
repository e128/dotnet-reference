using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ArgumentTokenizerBranchCoverageGapTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheArgumentsValueIsANullLiteral()
    {
        return CodeFixTestRunner.NoFixAsync<ProcessStartInfoArgumentAnalyzer, ProcessStartInfoArgumentCodeFixProvider>(
            """
            using System.Diagnostics;
            class C
            {
                void M()
                {
                    var startInfo = new ProcessStartInfo
                    {
                        {|E128091:Arguments = null|}
                    };
                }
            }
            """);
    }
}
