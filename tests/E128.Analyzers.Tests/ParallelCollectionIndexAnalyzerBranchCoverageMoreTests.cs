using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ParallelCollectionIndexAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string body)
    {
        var code = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n"
                   + "using static System.Linq.Enumerable;\n\n"
                   + "public static class Subject\n{\n"
                   + "    public static void Verify(List<string> columns, List<List<string>> rows, int k)\n    {\n"
                   + body
                   + "    }\n}\n";
        return AnalyzerTestRunner.VerifyAsync<ParallelCollectionIndexAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRangeStartIsNotALiteral()
    {
        return VerifyAsync("""
                    _ = Range(k, columns.Count).Select(i => rows[i]).ToList();

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheForLoopDeclaresNoVariable()
    {
        return VerifyAsync("""
                    int i;
                    for (i = 0; i < columns.Count; i++)
                    {
                        _ = rows[i];
                    }

            """);
    }
}
