using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ParallelForPerItemBodyCodeFixBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ForeignPartitionerInScope_OffersNoAction()
    {
        return CodeFixTestRunner.NoFixAsync<ParallelForPerItemBodyAnalyzer, ParallelForPerItemBodyCodeFixProvider>(
            """
            using System.Threading.Tasks;
            class Partitioner
            {
            }

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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_MethodGroupParameterNameInScope_AppendsAnIndex()
    {
        return CodeFixTestRunner.RunAsync<ParallelForPerItemBodyAnalyzer, ParallelForPerItemBodyCodeFixProvider>(
            """
            using System.Threading.Tasks;
            class C
            {
                int[] items = new int[] { 1, 2, 3 };

                void M()
                {
                    var value = 5;
                    {|E128233:Parallel.For(0, items.Length, Work)|};
                    Work(value);
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
                    var value = 5;
                    Parallel.ForEach(Partitioner.Create(0, items.Length), range =>
                    {
                        for (int value1 = range.Item1; value1 < range.Item2; value1++)
                        {
                            Work(value1);
                        }
                    });
                    Work(value);
                }

                void Work(int value)
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_MethodGroupParameterNamedRange_RenamesTheLoopVariable()
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

                void Work(int range)
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
                        for (int range1 = range.Item1; range1 < range.Item2; range1++)
                        {
                            Work(range1);
                        }
                    });
                }

                void Work(int range)
                {
                }
            }
            """);
    }
}
