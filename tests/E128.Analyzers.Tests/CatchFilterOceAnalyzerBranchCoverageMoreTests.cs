using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CatchFilterOceAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFilterNamesATypeThroughANamespaceQualifier()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterOceAnalyzer>(
            """
            using System;
            class C
            {
                void M()
                {
                    try { }
                    {|E128039:catch (Exception ex) when (ex is not System.IO.IOException)|}
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFilterQualifiesTheCancellationType()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterOceAnalyzer>(
            """
            using System;
            class C
            {
                void M()
                {
                    try { }
                    catch (Exception ex) when (ex is not System.OperationCanceledException)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenASingleNotPatternExcludesAnUnrelatedType()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterOceAnalyzer>(
            """
            using System;
            class C
            {
                void M()
                {
                    try { }
                    {|E128039:catch (Exception ex) when (ex is not OutOfMemoryException)|}
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenANestedBinaryPatternPrecedesTheNotPattern()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterOceAnalyzer>(
            """
            using System;
            using System.IO;
            class C
            {
                void M()
                {
                    try { }
                    {|E128039:catch (Exception ex) when (ex is IOException or FileNotFoundException or not OutOfMemoryException)|}
                    {
                    }
                }
            }
            """);
    }
}
