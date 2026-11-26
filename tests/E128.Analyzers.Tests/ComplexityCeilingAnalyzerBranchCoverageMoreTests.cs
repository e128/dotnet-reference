using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;
using static E128.Analyzers.Tests.ComplexityCeilingTestHarness;

namespace E128.Analyzers.Tests;

public sealed class ComplexityCeilingAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Flagged_WhenTheInnerBoundIsAPrimaryConstructorParameterFromAnotherFile()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            partial class C
            {
                void M(int[] items)
                {
                    foreach (var item in items)
                    {
                        foreach (var value in data)
                        {
                            _ = item + value;
                        }
                    }
                }
            }
            """,
            Warning(7, 13),
            new AnalyzerTestOptions { Sources = ["partial class C(int[] data)\n{\n}\n"] });
    }
}
