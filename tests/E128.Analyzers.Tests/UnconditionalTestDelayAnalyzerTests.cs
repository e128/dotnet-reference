using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnconditionalTestDelayAnalyzerTests
{

    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<UnconditionalTestDelayAnalyzer>(
            code,
            options: new AnalyzerTestOptions { References = Net100WithXunit });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenFactCallsThreadSleep()
    {
        return VerifyAsync("""
            using System.Threading;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public void Should_WriteTheValue()
                {
                    {|E128236:Thread.Sleep(500)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenFactCallsThreadSleepWithTimeSpan()
    {
        return VerifyAsync("""
            using System;
            using System.Threading;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public void Should_WriteTheValue()
                {
                    {|E128236:Thread.Sleep(TimeSpan.FromSeconds(1))|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenFactAwaitsTaskDelayLiteral()
    {
        return VerifyAsync("""
            using System.Threading.Tasks;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public async Task Should_RaiseTheSignal()
                {
                    await {|E128236:Task.Delay(500)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenFactAwaitsTaskDelayTimeSpanFactory()
    {
        return VerifyAsync("""
            using System;
            using System.Threading.Tasks;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public async Task Should_RaiseTheSignal()
                {
                    await {|E128236:Task.Delay(TimeSpan.FromMilliseconds(200))|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenFactCallsSpinWaitSpinUntil()
    {
        return VerifyAsync("""
            using System.Threading;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public void Should_RaiseTheSignal()
                {
                    var flag = false;
                    {|E128236:SpinWait.SpinUntil(() => flag)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheoryAwaitsTaskDelayLiteral()
    {
        return VerifyAsync("""
            using System.Threading.Tasks;
            using Xunit;

            public sealed class Subject
            {
                [Theory]
                [InlineData(1)]
                public async Task Should_RaiseTheSignal(int count)
                {
                    await {|E128236:Task.Delay(500)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_EachDelayCallInOneTestMethod()
    {
        return VerifyAsync("""
            using System.Threading;
            using System.Threading.Tasks;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public async Task Should_RaiseTheSignal()
                {
                    {|E128236:Thread.Sleep(100)|};
                    await {|E128236:Task.Delay(100)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTaskDelayTakesCancellationToken()
    {
        return VerifyAsync("""
            using System.Threading;
            using System.Threading.Tasks;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public async Task Should_RaiseTheSignal()
                {
                    await Task.Delay(500, CancellationToken.None);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTaskDelayTakesAVariableDuration()
    {
        return VerifyAsync("""
            using System.Threading.Tasks;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public async Task Should_RaiseTheSignal()
                {
                    var milliseconds = 500;
                    await Task.Delay(milliseconds);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThreadSleepSitsInsideForLoop()
    {
        return VerifyAsync("""
            using System.Threading;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public void Should_WriteEveryValue()
                {
                    for (var i = 0; i < 10; i++)
                    {
                        Thread.Sleep(500);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTaskDelaySitsInsideWhileLoop()
    {
        return VerifyAsync("""
            using System.Threading.Tasks;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public async Task Should_RaiseTheSignal()
                {
                    while (Subject.Ready())
                    {
                        await Task.Delay(500);
                    }
                }

                private static bool Ready() => true;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenDelaySitsInANonTestMethod()
    {
        return VerifyAsync("""
            using System.Threading;
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public void Should_WriteTheValue()
                {
                }

                private void WaitQuietly()
                {
                    Thread.Sleep(500);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAUserTypeOwnsSleepAndDelay()
    {
        return VerifyAsync("""
            using System.Threading.Tasks;
            using Xunit;

            public sealed class Clock
            {
                public void Sleep(int milliseconds) { }

                public Task Delay(int milliseconds) => Task.CompletedTask;
            }

            public sealed class Subject
            {
                private readonly Clock clock = new();

                [Fact]
                public async Task Should_WriteTheValue()
                {
                    clock.Sleep(500);
                    await clock.Delay(500);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheDelayNameFailsToBind()
    {
        return VerifyAsync("""
            using Xunit;

            public sealed class Subject
            {
                [Fact]
                public void Should_WriteTheValue()
                {
                    {|CS0103:Thread|}.Sleep(500);
                }
            }
            """);
    }
}
