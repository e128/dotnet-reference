using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;
using static E128.Analyzers.Tests.ComplexityCeilingTestHarness;

namespace E128.Analyzers.Tests;

public sealed class ComplexityCeilingBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Flagged_WhenWhileConditionDeclaresTheElementTheInnerLoopWalks()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            using System.Collections.Generic;
            class C
            {
                void M(Queue<int[]> queue)
                {
                    while (queue.TryDequeue(out var item))
                    {
                        foreach (var value in item)
                        {
                            _ = value;
                        }
                    }
                }
            }
            """,
            Warning(8, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Silent_WhenInnerBoundIndexesTheOuterCounterThroughAnIndexer()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            using System.Collections.Generic;
            class C
            {
                void M(List<List<int>> rows)
                {
                    for (var i = 0; i < rows.Count; i++)
                    {
                        for (var j = 0; j < rows[i].Count; j++)
                        {
                            _ = rows[i][j];
                        }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Silent_WhenInnerWhileWalksAVariableDeclaredInTheOuterBody()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                void M(string[] rows)
                {
                    foreach (var row in rows)
                    {
                        var k = 0;
                        while (k < row.Length)
                        {
                            k++;
                        }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Flagged_WhenForeachRunsInsideAWhileLoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                void M(int[] items, bool again)
                {
                    while (again)
                    {
                        foreach (var item in items)
                        {
                            again = item > 0;
                        }
                    }
                }
            }
            """,
            Warning(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Flagged_WhenOuterForUsesNotEqualsBound()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                void M(int[] items)
                {
                    for (var i = 0; i != 10; i++)
                    {
                        foreach (var item in items)
                        {
                            _ = item + i;
                        }
                    }
                }
            }
            """,
            Warning(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Silent_WhenOuterForHasConstantOnTheLeft()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                void M(int[] items)
                {
                    for (var i = 10; 0 < i; i--)
                    {
                        foreach (var item in items)
                        {
                            _ = item + i;
                        }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Flagged_WhenOuterForHasNoCondition()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                void M(int[] items)
                {
                    for (var i = 0; ; i++)
                    {
                        foreach (var item in items)
                        {
                            _ = item;
                        }

                        if (i > 3)
                        {
                            break;
                        }
                    }
                }
            }
            """,
            Warning(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Silent_WhenOuterForeachRunsOverAFixedSizeArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                void M(int[] items)
                {
                    foreach (var fixedItem in new int[4])
                    {
                        foreach (var item in items)
                        {
                            _ = item + fixedItem;
                        }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Flagged_WhenOuterForeachRunsOverAnArrayWithVariableSize()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                void M(int[] items, int size)
                {
                    foreach (var sized in new int[size])
                    {
                        foreach (var item in items)
                        {
                            _ = item + sized;
                        }
                    }
                }
            }
            """,
            Warning(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_Flagged_WhenOuterForeachRunsOverAMultiDimensionalArrayCreation()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                void M(int[] items)
                {
                    foreach (var cell in new int[2, 3])
                    {
                        foreach (var item in items)
                        {
                            _ = item + cell;
                        }
                    }
                }
            }
            """,
            Warning(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Loop_ReportsCubicOrWorse_WhenFourLoopsNest()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                void M(int[] items)
                {
                    foreach (var a in items)
                    {
                        foreach (var b in items)
                        {
                            foreach (var c in items)
                            {
                                foreach (var d in items)
                                {
                                    _ = a + b + c + d;
                                }
                            }
                        }
                    }
                }
            }
            """,
            [Warning(7, 13), Warning(9, 17), Warning(11, 21)]);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Silent_WhenCeilingIsExponential()
    {
        return VerifyWithCeilingAsync(
            "exponential",
            """
            class C
            {
                int Fib(int n)
                {
                    return n < 2 ? n : Fib(n - 1) + Fib(n - 2);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Silent_WhenArgumentsComeFromAnUnknownCall()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                int F(int n)
                {
                    return n < 2 ? n : F(Next(n)) + F(Next(n));
                }

                static int Next(int n) => n - 1;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Flagged_WhenAnUnknownArgumentSitsBesideASubtractiveOne()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                int F(int a, int b)
                {
                    return a < 2 ? a : F(Next(a), b - 1) + F(a, b - 1);
                }

                static int Next(int n) => n / 2;
            }
            """,
            Warning(3, 5));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Silent_WhenEveryArgumentIsDivisiveOrUnchanged()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                int F(int a, int b)
                {
                    return a < 2 ? a : F(a / 2, b / 2) + F(a, b / 2) + F(a, b);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Silent_WhenBinaryArgumentDoesNotStartWithTheParameter()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                int F(int n)
                {
                    return n < 2 ? n : F(1 + n) + F(2 * n);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Flagged_WhenAnIntegerAddsANegativeConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                int F(int n)
                {
                    return n < 2 ? n : F(n + -1) + F(n + -2);
                }
            }
            """,
            Warning(3, 5));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Flagged_WhenALongAddsANegativeConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                long F(long n)
                {
                    return n < 2 ? n : F(n + -1L) + F(n + -2L);
                }
            }
            """,
            Warning(3, 5));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Flagged_WhenADoubleAddsANegativeConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                double F(double x)
                {
                    return x < 2 ? x : F(x + -1.0) + F(x + -2.0);
                }
            }
            """,
            Warning(3, 5));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Silent_WhenConstantsAreNotNegative()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                int F(int n)
                {
                    return n < 2 ? n : F(n + 1) + F(n + 2);
                }

                long G(long n)
                {
                    return n < 2 ? n : G(n + 1L) + G(n + 2L);
                }

                double H(double x)
                {
                    return x < 2 ? x : H(x + 1.0) + H(x + 2.0);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Silent_WhenAddedOperandIsNotConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                int F(int n, int step)
                {
                    return n < 2 ? n : F(n + step, step) + F(n + step, step);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Silent_WhenDivisorTypesAreLongOrDouble()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                long F(long n)
                {
                    return n < 2 ? n : F(n / 2L) + F(n / 3L);
                }

                double G(double x)
                {
                    return x < 2 ? x : G(x / 2.0) + G(x / 4.0);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Silent_WhenDivisorIsBelowTwoOrOfAnotherType()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                int F(int n)
                {
                    return n < 2 ? n : F(n / 1) + F(n / 1);
                }

                uint G(uint n)
                {
                    return n < 2 ? n : G(n / 2u) + G(n / 2u);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Flagged_WhenLongAndDoubleSubtractConstants()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                long F(long n)
                {
                    return n < 2 ? n : F(n - 1) + F(n - 2);
                }

                double G(double x)
                {
                    return x < 2 ? x : G(x - 1.0) + G(x - 2.0);
                }
            }
            """,
            Warning(3, 5),
            Warning(8, 5));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Silent_WhenTheCacheIsIncrementedAndRead()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                int[] seen = new int[64];

                int F(int n)
                {
                    if (seen[n] > 0)
                    {
                        return seen[n];
                    }

                    seen[n]++;
                    return F(n - 1) + F(n - 2);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Flagged_WhenTheCacheBelongsToAnotherType()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class Cache
            {
                public static int[] Values = new int[64];
            }

            class C
            {
                int F(int n)
                {
                    Cache.Values[n] = F(n - 1) + F(n - 2);
                    return Cache.Values[n];
                }
            }
            """,
            Warning(8, 5));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Recursion_Flagged_WhenTheFieldIsNotACollection()
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            """
            class C
            {
                int total;

                int F(int n)
                {
                    total = F(n - 1) + F(n - 2);
                    return total;
                }
            }
            """,
            Warning(5, 5));
    }
}
