using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CatchFilterOceBranchCoverageTests
{
    private static Task VerifyAsync(string catches)
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterOceAnalyzer>(
            "using System;\nusing System.IO;\nusing System.Threading.Tasks;\nclass C\n{\n    void M(bool flag)\n    {\n        try { }\n"
            + catches + "\n    }\n}\n");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("ex is not OutOfMemoryException and not IOException")]
    [InlineData("ex is not (OutOfMemoryException or IOException)")]
    [InlineData("ex is IOException or not OutOfMemoryException")]
    public Task Reports_ForFiltersThatExcludeTypesWithoutTheCancellationType(string filter)
    {
        return VerifyAsync($$"""
                    {|E128039:catch (Exception ex) when ({{filter}})|}
                    {
                    }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("ex is IOException or FileNotFoundException")]
    [InlineData("flag")]
    [InlineData("ex.Message.Length > 0")]
    [InlineData("ex is IOException")]
    public Task Silent_ForFiltersThatAreNotExclusionPatterns(string filter)
    {
        return VerifyAsync($$"""
                    catch (Exception ex) when ({{filter}})
                    {
                    }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("OperationCanceledException")]
    [InlineData("TaskCanceledException")]
    public Task Silent_WhenAnEarlierCatchHandlesCancellation(string handled)
    {
        return VerifyAsync($$"""
                    catch ({{handled}})
                    {
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                    }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenEarlierCatchesAreUnrelatedOrFiltered()
    {
        return VerifyAsync("""
                    catch (IOException)
                    {
                    }
                    catch when (flag)
                    {
                    }
                    {|E128039:catch (Exception ex) when (ex is not OutOfMemoryException)|}
                    {
                    }
            """);
    }
}
