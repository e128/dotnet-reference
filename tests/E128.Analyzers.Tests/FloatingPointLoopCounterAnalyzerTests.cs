using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FloatingPointLoopCounterAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAFloatCounter()
    {
        return AnalyzerTestRunner.VerifyAsync<FloatingPointLoopCounterAnalyzer>("""
            class C
            {
                void M()
                {
                    for (float {|E128248:i|} = 0; i < 1; i += 0.1f)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForADoubleCounter()
    {
        return AnalyzerTestRunner.VerifyAsync<FloatingPointLoopCounterAnalyzer>("""
            class C
            {
                void M()
                {
                    for (double {|E128248:d|} = 0; d < 1; d += 0.1)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnInferredDoubleCounter()
    {
        return AnalyzerTestRunner.VerifyAsync<FloatingPointLoopCounterAnalyzer>("""
            class C
            {
                void M()
                {
                    for (var {|E128248:i|} = 0.0; i < 1; i += 0.1)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAPredeclaredCounter()
    {
        return AnalyzerTestRunner.VerifyAsync<FloatingPointLoopCounterAnalyzer>("""
            class C
            {
                void M()
                {
                    double i;
                    for ({|E128248:i|} = 0; i < 1; i += 0.1)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnIncrementorOnlyCounter()
    {
        return AnalyzerTestRunner.VerifyAsync<FloatingPointLoopCounterAnalyzer>("""
            class C
            {
                void M()
                {
                    double i = 0;
                    for (; i < 1; {|E128248:i|} += 0.1)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnIntegerCounter()
    {
        return AnalyzerTestRunner.VerifyAsync<FloatingPointLoopCounterAnalyzer>("""
            class C
            {
                void M()
                {
                    for (int i = 0; i < 10; i++)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForADecimalCounter()
    {
        return AnalyzerTestRunner.VerifyAsync<FloatingPointLoopCounterAnalyzer>("""
            class C
            {
                void M()
                {
                    for (decimal d = 0; d < 1; d += 0.1m)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAForeachLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<FloatingPointLoopCounterAnalyzer>("""
            class C
            {
                void M(double[] values)
                {
                    foreach (var value in values)
                    {
                    }
                }
            }
            """);
    }
}
