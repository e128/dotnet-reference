using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsDirectInstantiationAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WithTheGenericAdviceWhenTheConstructorTakesTwoArguments()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsDirectInstantiationAnalyzer>(
            """
            namespace System.Security.Cryptography
            {
                public class TwinCng
                {
                    public TwinCng(int first, int second) { }
                }
            }

            class C
            {
                void M()
                {
                    var a = {|E128142:new System.Security.Cryptography.TwinCng(1, 2)|};
                }
            }
            """);
    }
}
