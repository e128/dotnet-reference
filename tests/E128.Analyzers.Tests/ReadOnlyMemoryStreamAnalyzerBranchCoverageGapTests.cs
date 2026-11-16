using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ReadOnlyMemoryStreamAnalyzerBranchCoverageGapTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStreamHasAnInitializerAndNoArgumentList()
    {
        return AnalyzerTestRunner.VerifyAsync<ReadOnlyMemoryStreamAnalyzer>(
            """
            using System.IO;
            class C
            {
                void M()
                {
                    using var stream = new MemoryStream { Capacity = 4 };
                    _ = stream.Length;
                }
            }
            """);
    }
}
