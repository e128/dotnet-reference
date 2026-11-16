using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PollingLoopNoTokenBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_ForDelayInAForeachOrDoLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<PollingLoopNoTokenAnalyzer>("""
            using System;
            using System.Threading.Tasks;
            class C
            {
                async Task M(int[] items, Func<bool> done)
                {
                    foreach (var item in items)
                    {
                        await {|E128211:Task.Delay(item)|};
                    }

                    do
                    {
                        await {|E128211:Task.Delay(TimeSpan.FromSeconds(1))|};
                    }
                    while (!done());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_ForDelayCalledThroughUsingStatic()
    {
        return AnalyzerTestRunner.VerifyAsync<PollingLoopNoTokenAnalyzer>("""
            using System;
            using System.Threading.Tasks;
            using static System.Threading.Tasks.Task;
            class C
            {
                async Task M(Func<bool> done)
                {
                    while (!done())
                    {
                        await {|E128211:Delay(100)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopConditionObservesTheToken()
    {
        return AnalyzerTestRunner.VerifyAsync<PollingLoopNoTokenAnalyzer>("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                static bool Check(CancellationToken token) => token.IsCancellationRequested;

                async Task M(CancellationToken token)
                {
                    for (; !token.IsCancellationRequested;)
                    {
                        await Task.Delay(100);
                    }

                    while (!Check(token))
                    {
                        await Task.Delay(100);
                    }

                    do
                    {
                        await Task.Delay(100);
                    }
                    while (!token.IsCancellationRequested);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenTheLoopConditionMentionsNoToken()
    {
        return AnalyzerTestRunner.VerifyAsync<PollingLoopNoTokenAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                async Task M(int attempts)
                {
                    for (var i = 0; i < attempts; i++)
                    {
                        await {|E128211:Task.Delay(100)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForDelayCalledOnAnotherReceiverExpression()
    {
        return AnalyzerTestRunner.VerifyAsync<PollingLoopNoTokenAnalyzer>("""
            using System;
            using System.Threading.Tasks;
            class C
            {
                void M(Func<Func<int, Task>> factory, int[] items)
                {
                    foreach (var item in items)
                    {
                        _ = factory()(item);
                    }
                }
            }
            """);
    }
}
