using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ParallelForPerItemBodyAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ExpressionBody()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    {|E128233:Parallel.For(0, items.Length, i => total += items[i])|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_BlockBody()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    {|E128233:Parallel.For(0, items.Length, i => { total += items[i]; })|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_QualifiedReceiver()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    {|E128233:System.Threading.Tasks.Parallel.For(0, items.Length, i => total += items[i])|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenANestedLambdaCapturesTheParameter()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            class C
            {
                List<Action> jobs = new List<Action>();
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    {|E128233:Parallel.For(0, items.Length, i => { jobs.Add(() => Work(items[i])); })|};
                }

                void Work(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyContainsALoop()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, i => { for (int j = 0; j < 3; j++) { total += j; } });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyAwaits()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, async i => { await Task.Delay(10); });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAUserTypeDeclaresItsOwnFor()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System;
            class Parallel
            {
                public static void For(int fromInclusive, int toExclusive, Action<int> body)
                {
                }
            }
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, i => total += items[i]);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheForTLocalOverloadRuns()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, () => 0L, (i, state, local) => local + items[i], local => { });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLongOverloadRuns()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0L, items.LongLength, i => Work(i));
                }

                void Work(long value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyTakesALoopState()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, (i, state) => { total += items[i]; });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyAssignsTheParameter()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, i => { i = i + 1; total += i; });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyIncrementsTheParameter()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, i => { total += items[i]; i++; });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyPassesTheParameterByReference()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, i => Adjust(ref i));
                }

                void Adjust(ref int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyIsAnAnonymousMethod()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, delegate (int i) { total += items[i]; });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheParameterIsNamedRange()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, range => { total += items[range]; });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyUsesTheNameRange()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int total;
                int range;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, i => total += range);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallUsesNamedArguments()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(fromInclusive: 0, toExclusive: items.Length, body: i => total += items[i]);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheExpressionBodyFitsNoStatement()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, i => {|CS0201:items[i]|});
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBlockBodyIsEmpty()
    {
        return AnalyzerTestRunner.VerifyAsync<ParallelForPerItemBodyAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.For(0, items.Length, i => { });
                }
            }
            """);
    }
}
