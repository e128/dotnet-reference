using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ParallelForPerItemBodyCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ExpressionBody_AddsConcurrentUsing()
    {
        return CodeFixTestRunner.RunAsync<ParallelForPerItemBodyAnalyzer, ParallelForPerItemBodyCodeFixProvider>(
            """
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    {|E128233:Parallel.For(0, items.Length, i => Work(items[i]))|};
                }

                void Work(int value)
                {
                }
            }
            """,
            """
            using System.Collections.Concurrent;
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.ForEach(Partitioner.Create(0, items.Length), range =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            Work(items[i]);
                        }
                    });
                }

                void Work(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_NamedArguments_UsesParameterOrder()
    {
        return CodeFixTestRunner.RunAsync<ParallelForPerItemBodyAnalyzer, ParallelForPerItemBodyCodeFixProvider>(
            """
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    {|E128233:Parallel.For(fromInclusive: 0, toExclusive: items.Length, body: i => Work(items[i]))|};
                }

                void Work(int value)
                {
                }
            }
            """,
            """
            using System.Collections.Concurrent;
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.ForEach(Partitioner.Create(0, items.Length), range =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            Work(items[i]);
                        }
                    });
                }

                void Work(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_MethodGroup_RewritesAsAnInvocation()
    {
        return CodeFixTestRunner.RunAsync<ParallelForPerItemBodyAnalyzer, ParallelForPerItemBodyCodeFixProvider>(
            """
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    {|E128233:Parallel.For(0, items.Length, Work)|};
                }

                void Work(int value)
                {
                }
            }
            """,
            """
            using System.Collections.Concurrent;
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.ForEach(Partitioner.Create(0, items.Length), range =>
                    {
                        for (int value = range.Item1; value < range.Item2; value++)
                        {
                            Work(value);
                        }
                    });
                }

                void Work(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_BlockBody_AddsConcurrentUsing()
    {
        return CodeFixTestRunner.RunAsync<ParallelForPerItemBodyAnalyzer, ParallelForPerItemBodyCodeFixProvider>(
            """
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
            """,
            """
            using System.Collections.Concurrent;
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.ForEach(Partitioner.Create(0, items.Length), range =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            total += items[i];
                        }
                    });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ExpressionBody_KeepsExistingConcurrentUsing()
    {
        return CodeFixTestRunner.RunAsync<ParallelForPerItemBodyAnalyzer, ParallelForPerItemBodyCodeFixProvider>(
            """
            using System.Collections.Concurrent;
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    {|E128233:Parallel.For(0, items.Length, i => Work(items[i]))|};
                }

                void Work(int value)
                {
                }
            }
            """,
            """
            using System.Collections.Concurrent;
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.ForEach(Partitioner.Create(0, items.Length), range =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            Work(items[i]);
                        }
                    });
                }

                void Work(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_BlockBody_KeepsExistingConcurrentUsing()
    {
        return CodeFixTestRunner.RunAsync<ParallelForPerItemBodyAnalyzer, ParallelForPerItemBodyCodeFixProvider>(
            """
            using System.Collections.Concurrent;
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
            """,
            """
            using System.Collections.Concurrent;
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.ForEach(Partitioner.Create(0, items.Length), range =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            total += items[i];
                        }
                    });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_QualifiedReceiver_KeepsTheReceiverText()
    {
        return CodeFixTestRunner.RunAsync<ParallelForPerItemBodyAnalyzer, ParallelForPerItemBodyCodeFixProvider>(
            """
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    {|E128233:System.Threading.Tasks.Parallel.For(0, items.Length, i => total += items[i])|};
                }
            }
            """,
            """
            using System.Collections.Concurrent;
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    System.Threading.Tasks.Parallel.ForEach(Partitioner.Create(0, items.Length), range =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            total += items[i];
                        }
                    });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_LongOverload_UsesALongLoopVariable()
    {
        return CodeFixTestRunner.RunAsync<ParallelForPerItemBodyAnalyzer, ParallelForPerItemBodyCodeFixProvider>(
            """
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    {|E128233:Parallel.For(0L, items.LongLength, i => Work(i))|};
                }

                void Work(long value)
                {
                }
            }
            """,
            """
            using System.Collections.Concurrent;
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.ForEach(Partitioner.Create(0L, items.LongLength), range =>
                    {
                        for (long i = range.Item1; i < range.Item2; i++)
                        {
                            Work(i);
                        }
                    });
                }

                void Work(long value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_LoopStateOverload_KeepsTheStateParameter()
    {
        return CodeFixTestRunner.RunAsync<ParallelForPerItemBodyAnalyzer, ParallelForPerItemBodyCodeFixProvider>(
            """
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    {|E128233:Parallel.For(0, items.Length, (i, state) => { total += items[i]; })|};
                }
            }
            """,
            """
            using System.Collections.Concurrent;
            using System.Threading.Tasks;
            class C
            {
                int total;
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    Parallel.ForEach(Partitioner.Create(0, items.Length), (range, state) =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            total += items[i];
                        }
                    });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AsyncBody_OffersNoAction()
    {
        return CodeFixTestRunner.NoFixAsync<ParallelForPerItemBodyAnalyzer, ParallelForPerItemBodyCodeFixProvider>(
            """
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    {|E128233:Parallel.For(0, items.Length, async i => { await WorkAsync(items[i]); })|};
                }

                Task WorkAsync(int value) => Task.CompletedTask;
            }
            """);
    }
}
