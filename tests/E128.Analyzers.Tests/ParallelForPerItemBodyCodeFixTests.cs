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
}
