using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ComplexityCeilingBranchCoverageGapTests
{
    private static DiagnosticResult Warning(int line, int column)
    {
        return new DiagnosticResult("E128152", DiagnosticSeverity.Warning).WithLocation(line, column);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Flagged_WhenTheInnerForHasNoCondition()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                void M(int[] items)
                {
                    foreach (var item in items)
                    {
                        for (;;)
                        {
                            break;
                        }
                    }
                }
            }
            """,
            Warning(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Flagged_WhenTheInnerBoundPassesTheOuterCounterToACall()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                static int Limit(int index) => index;

                void M(int count)
                {
                    for (var i = 0; i < count; i++)
                    {
                        for (var j = 0; j < Limit(i); j++)
                        {
                            _ = j;
                        }
                    }
                }
            }
            """,
            Warning(9, 13));
    }

}
