using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ComplexityCeilingAnalyzerBranchCoverageMoreTests
{
    private static DiagnosticResult Warning(int line, int column)
    {
        return new DiagnosticResult("E128152", DiagnosticSeverity.Warning).WithLocation(line, column);
    }

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
