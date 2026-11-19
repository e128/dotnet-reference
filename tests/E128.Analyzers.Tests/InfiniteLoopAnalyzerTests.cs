using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InfiniteLoopAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForWhileTrueWithNoExit()
    {
        return AnalyzerTestRunner.VerifyAsync<InfiniteLoopAnalyzer>("""
            class C
            {
                void M()
                {
                    {|E128251:while|} (true)
                    {
                        Work();
                    }
                }

                static void Work()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForForLoopWithNoCondition()
    {
        return AnalyzerTestRunner.VerifyAsync<InfiniteLoopAnalyzer>("""
            class C
            {
                void M()
                {
                    {|E128251:for|} (;;)
                    {
                        Work();
                    }
                }

                static void Work()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForDoWhileTrueWithNoExit()
    {
        return AnalyzerTestRunner.VerifyAsync<InfiniteLoopAnalyzer>("""
            class C
            {
                void M()
                {
                    {|E128251:do|}
                    {
                        Work();
                    }
                    while (true);

                    static void Work()
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheBreakTargetsANestedSwitch()
    {
        return AnalyzerTestRunner.VerifyAsync<InfiniteLoopAnalyzer>("""
            class C
            {
                void M(int value)
                {
                    {|E128251:while|} (true)
                    {
                        switch (value)
                        {
                            case 1:
                                break;
                        }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForABreak()
    {
        return AnalyzerTestRunner.VerifyAsync<InfiniteLoopAnalyzer>("""
            class C
            {
                void M(bool stop)
                {
                    while (true)
                    {
                        if (stop)
                        {
                            break;
                        }

                        Work();
                    }
                }

                static void Work()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAReturn()
    {
        return AnalyzerTestRunner.VerifyAsync<InfiniteLoopAnalyzer>("""
            class C
            {
                int M(bool stop)
                {
                    while (true)
                    {
                        if (stop)
                        {
                            return 0;
                        }
                    }

                    return 1;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAThrow()
    {
        return AnalyzerTestRunner.VerifyAsync<InfiniteLoopAnalyzer>("""
            using System;

            class C
            {
                void M(bool stop)
                {
                    while (true)
                    {
                        if (stop)
                        {
                            throw new InvalidOperationException();
                        }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForEnvironmentExit()
    {
        return AnalyzerTestRunner.VerifyAsync<InfiniteLoopAnalyzer>("""
            using System;

            class C
            {
                void M()
                {
                    while (true)
                    {
                        Environment.Exit(0);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForCancellationRequest()
    {
        return AnalyzerTestRunner.VerifyAsync<InfiniteLoopAnalyzer>("""
            using System.Threading;

            class C
            {
                void M(CancellationToken cancellationToken)
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Work();
                    }
                }

                static void Work()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAConditionalLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<InfiniteLoopAnalyzer>("""
            class C
            {
                void M(bool running)
                {
                    while (running)
                    {
                        Work();
                    }
                }

                static void Work()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheBodyHoldsOnlyALambda()
    {
        return AnalyzerTestRunner.VerifyAsync<InfiniteLoopAnalyzer>("""
            using System;

            class C
            {
                void M()
                {
                    {|E128251:while|} (true)
                    {
                        Action stop = () => { };
                        stop();
                    }
                }
            }
            """);
    }
}
