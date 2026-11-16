using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ParallelCollectionIndexBranchCoverageTests
{
    private static Task VerifyAsync(string body, string members = "")
    {
        var code = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n"
                   + "using static System.Linq.Enumerable;\n\n"
                   + "public static class Subject\n{\n"
                   + members
                   + "    public static void Verify(List<string> columns, List<List<string>> rows, int[,] grid, int k)\n    {\n"
                   + body
                   + "    }\n}\n";
        return AnalyzerTestRunner.VerifyAsync<ParallelCollectionIndexAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MultiArgumentIndexer_Silent()
    {
        return VerifyAsync("""
                    for (var i = 0; i < columns.Count; i++)
                    {
                        _ = grid[i, 0];
                    }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonIdentifierIndex_Silent()
    {
        return VerifyAsync("""
                    for (var i = 0; i < columns.Count; i++)
                    {
                        _ = rows[i + 1];
                    }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IndexThatNoLoopOrLambdaDeclares_Silent()
    {
        return VerifyAsync("""
                    _ = rows[k];

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TwoAccessesInOneLambda_BothReported()
    {
        return VerifyAsync("""
                    var cells = Enumerable.Range(0, columns.Count)
                        .Select(i => {|E128106:rows[i]|}.Count + {|E128106:rows[i]|}.Count)
                        .ToList();

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RangeThatStartsAboveZero_Silent()
    {
        return VerifyAsync("""
                    var cells = Enumerable.Range(1, columns.Count).Select(i => rows[i]).ToList();

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RangeWithConstantOrCapacityCount_Silent()
    {
        return VerifyAsync("""
                    var fixedCells = Enumerable.Range(0, 5).Select(i => rows[i]).ToList();
                    var sized = Enumerable.Range(0, columns.Capacity).Select(i => rows[i]).ToList();

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RepeatInsteadOfRange_Silent()
    {
        return VerifyAsync("""
                    var cells = Enumerable.Repeat(0, columns.Count).Select((x, i) => rows[i]).ToList();

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LambdaOutsideAnyInvocation_Silent()
    {
        return VerifyAsync("""
                    Func<int, List<string>> pick = i => rows[i];
                    _ = pick(0);

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RangeCalledThroughADelegateVariableOrArrayElement_Silent()
    {
        return VerifyAsync("""
                    Func<int, int, IEnumerable<int>> range = Enumerable.Range;
                    var viaVariable = range(0, columns.Count).Select(i => rows[i]).ToList();
                    var ranges = new[] { range };
                    var viaElement = ranges[0](0, columns.Count).Select(i => rows[i]).ToList();

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParenthesizedLambdaParameter_Reported()
    {
        return VerifyAsync("""
                    var cells = Enumerable.Range(0, columns.Count).Select((i) => {|E128106:rows[i]|}).ToList();

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RangeCalledThroughUsingStatic_Reported()
    {
        return VerifyAsync("""
                    var cells = Range(0, columns.Count).Select(i => {|E128106:rows[i]|}).ToList();

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AnonymousMethodParameter_Silent()
    {
        return VerifyAsync("""
                    var cells = Enumerable.Range(0, columns.Count)
                        .Select(delegate (int i) { return rows[i]; })
                        .ToList();

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedLambdaOverTheOuterIndex_Reported()
    {
        return VerifyAsync("""
                    var cells = Enumerable.Range(0, columns.Count)
                        .Select(i => columns.Select(x => {|E128106:rows[i]|}).ToList())
                        .ToList();

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LengthGuardWithTheIndexOnTheLeft_Silent()
    {
        return VerifyAsync("""
                    for (var i = 0; i < columns.Count; i++)
                    {
                        if (i < rows.Count)
                        {
                            _ = rows[i];
                        }
                    }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComparisonsThatAreNotLengthGuards_StillReported()
    {
        return VerifyAsync("""
                    for (var i = 0; i < columns.Count; i++)
                    {
                        if (k > i && i < columns.Count && rows.Count > 0 && GetRows().Count > i && new List<int>().Capacity > i)
                        {
                            _ = {|E128106:rows[i]|};
                        }
                    }

            """, """
                public static List<string> GetRows() => new();

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ForLoopWithoutCondition_Silent()
    {
        return VerifyAsync("""
                    for (var i = 0; ; i++)
                    {
                        _ = rows[i];
                        break;
                    }

            """);
    }
}
