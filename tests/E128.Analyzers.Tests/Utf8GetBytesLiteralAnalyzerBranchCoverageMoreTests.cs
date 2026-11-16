using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class Utf8GetBytesLiteralAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLiteralFeedsAConstructorInitializer()
    {
        return AnalyzerTestRunner.VerifyAsync<Utf8GetBytesLiteralAnalyzer>(
            """
            using System.Text;
            class Base
            {
                public Base(byte[] data) { }
            }

            class Derived : Base
            {
                public Derived() : base({|E128141:Encoding.UTF8.GetBytes("abc")|}) { }
            }
            """);
    }
}
