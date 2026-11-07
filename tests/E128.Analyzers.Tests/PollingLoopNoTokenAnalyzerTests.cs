using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PollingLoopNoTokenAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task DelayInWhileLoop_NoToken_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PollingLoopNoTokenAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                async Task M()
                {
                    while (true)
                    {
                        await {|E128211:Task.Delay(100)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DelayInForLoop_NoToken_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<PollingLoopNoTokenAnalyzer>("""
            using System.Collections.Generic;
            using System.Threading.Tasks;
            class C
            {
                async Task M(IReadOnlyList<string> items)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        await {|E128211:Task.Delay(100)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DelayWithToken_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<PollingLoopNoTokenAnalyzer>("""
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                async Task M(CancellationToken cancellationToken)
                {
                    while (true)
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DelayOutsideLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<PollingLoopNoTokenAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                Task M()
                {
                    return Task.Delay(100);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThreadSleepInLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<PollingLoopNoTokenAnalyzer>("""
            using System.Threading;
            class C
            {
                void M()
                {
                    while (true)
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CustomDelayInLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<PollingLoopNoTokenAnalyzer>("""
            using System.Threading.Tasks;
            class Timer
            {
                public static Task Delay(int milliseconds) => Task.CompletedTask;
            }

            class C
            {
                async Task M()
                {
                    while (true)
                    {
                        await Timer.Delay(100);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CancellationAwareCondition_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<PollingLoopNoTokenAnalyzer>("""
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                async Task M(CancellationToken cancellationToken)
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(100);
                    }
                }
            }
            """);
    }
}
