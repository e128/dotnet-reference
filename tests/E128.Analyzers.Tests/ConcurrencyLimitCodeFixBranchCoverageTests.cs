using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcurrencyLimitCodeFixBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheZeroCapacityOfABoundedChannel()
    {
        return CodeFixTestRunner.RunAsync<ConcurrencyLimitAnalyzer, ConcurrencyLimitCodeFixProvider>(
            """
            using System;
            using System.Threading.Channels;
            class C
            {
                Channel<int> M() => {|E128040:Channel.CreateBounded<int>(0)|};
            }
            """,
            """
            using System;
            using System.Threading.Channels;
            class C
            {
                Channel<int> M() => Channel.CreateBounded<int>(Environment.ProcessorCount);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheZeroInAnObjectInitializer()
    {
        return CodeFixTestRunner.RunAsync<ConcurrencyLimitAnalyzer, ConcurrencyLimitCodeFixProvider>(
            """
            using System;
            using System.Threading.Tasks;
            class C
            {
                ParallelOptions M() => new ParallelOptions { MaxDegreeOfParallelism = {|E128040:0|} };
            }
            """,
            """
            using System;
            using System.Threading.Tasks;
            class C
            {
                ParallelOptions M() => new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_ForANegativeLimit()
    {
        return CodeFixTestRunner.NoFixAsync<ConcurrencyLimitAnalyzer, ConcurrencyLimitCodeFixProvider>(
            """
            using System.Threading;
            class C
            {
                SemaphoreSlim M() => {|E128040:new SemaphoreSlim(-5)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheLimitIsAConstantName()
    {
        return CodeFixTestRunner.NoFixAsync<ConcurrencyLimitAnalyzer, ConcurrencyLimitCodeFixProvider>(
            """
            using System.Threading.Tasks;
            class C
            {
                const int None = 0;

                ParallelOptions M() => new ParallelOptions { MaxDegreeOfParallelism = {|E128040:None|} };
            }
            """);
    }
}
