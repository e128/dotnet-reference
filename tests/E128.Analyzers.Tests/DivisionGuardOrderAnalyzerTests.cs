using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DivisionGuardOrderAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardFollowsTheDivision()
    {
        return AnalyzerTestRunner.VerifyAsync<DivisionGuardOrderAnalyzer>("""
            class C
            {
                int M(int value, int divisor)
                {
                    var result = value / divisor;
                    if ({|E128244:divisor == 0|})
                    {
                        return 0;
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnInequalityGuard()
    {
        return AnalyzerTestRunner.VerifyAsync<DivisionGuardOrderAnalyzer>("""
            class C
            {
                int M(int value, int divisor)
                {
                    var result = value / divisor;
                    if ({|E128244:divisor != 0|})
                    {
                        return result;
                    }

                    return 0;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAZeroOnTheLeft()
    {
        return AnalyzerTestRunner.VerifyAsync<DivisionGuardOrderAnalyzer>("""
            class C
            {
                int M(int value, int divisor)
                {
                    var result = value / divisor;
                    if ({|E128244:0 == divisor|})
                    {
                        return 0;
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForModulo()
    {
        return AnalyzerTestRunner.VerifyAsync<DivisionGuardOrderAnalyzer>("""
            class C
            {
                int M(int value, int divisor)
                {
                    var result = value % divisor;
                    if ({|E128244:divisor == 0|})
                    {
                        return 0;
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForCompoundAssignment()
    {
        return AnalyzerTestRunner.VerifyAsync<DivisionGuardOrderAnalyzer>("""
            class C
            {
                int M(int value, int divisor)
                {
                    value /= divisor;
                    if ({|E128244:divisor == 0|})
                    {
                        return 0;
                    }

                    return value;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardComesFirst()
    {
        return AnalyzerTestRunner.VerifyAsync<DivisionGuardOrderAnalyzer>("""
            class C
            {
                int M(int value, int divisor)
                {
                    if (divisor == 0)
                    {
                        return 0;
                    }

                    return value / divisor;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAFloatingPointDivision()
    {
        return AnalyzerTestRunner.VerifyAsync<DivisionGuardOrderAnalyzer>("""
            class C
            {
                double M(double value, double divisor)
                {
                    var result = value / divisor;
                    if (divisor == 0)
                    {
                        return 0;
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardReadsAnotherValue()
    {
        return AnalyzerTestRunner.VerifyAsync<DivisionGuardOrderAnalyzer>("""
            class C
            {
                int M(int value, int divisor, int other)
                {
                    var result = value / divisor;
                    if (other == 0)
                    {
                        return 0;
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAPropertyDivisor()
    {
        return AnalyzerTestRunner.VerifyAsync<DivisionGuardOrderAnalyzer>("""
            class Holder
            {
                public int Count { get; set; }
            }

            class C
            {
                int M(Holder holder)
                {
                    var result = 10 / holder.Count;
                    if (holder.Count == 0)
                    {
                        return 0;
                    }

                    return result;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheDivisionSitsInsideALambda()
    {
        return AnalyzerTestRunner.VerifyAsync<DivisionGuardOrderAnalyzer>("""
            using System;

            class C
            {
                int M(int value, int divisor)
                {
                    Action divide = () => Console.WriteLine(value / divisor);
                    if (divisor == 0)
                    {
                        return 0;
                    }

                    divide();
                    return value;
                }
            }
            """);
    }
}
