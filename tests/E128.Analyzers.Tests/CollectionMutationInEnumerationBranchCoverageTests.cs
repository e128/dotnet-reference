using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CollectionMutationInEnumerationBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>(
            "using System.Collections.Concurrent;\nusing System.Collections.Generic;\n" + code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAParameterMutatedByABracelessStatement()
    {
        return VerifyAsync("""
            class C
            {
                void M(List<int> items)
                {
                    foreach (var item in {|E128155:items|})
                        items.Remove(item);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForADeconstructingForeach()
    {
        return VerifyAsync("""
            class C
            {
                void M(List<(int, int)> pairs)
                {
                    foreach (var (a, b) in {|E128155:pairs|})
                    {
                        pairs.Clear();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAConcurrentCollection()
    {
        return VerifyAsync("""
            class C
            {
                void M(ConcurrentBag<int> bag)
                {
                    foreach (var item in bag)
                    {
                        bag.Add(item);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAStringReceiver()
    {
        return VerifyAsync("""
            class C
            {
                void M(string text)
                {
                    foreach (var c in text)
                    {
                        _ = text.Insert(0, "x");
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopSourceIsAMethodCall()
    {
        return VerifyAsync("""
            class C
            {
                List<int> items = new();

                List<int> Get() => items;

                void M()
                {
                    foreach (var item in Get())
                    {
                        items.Add(item);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMutationIsAnAssignmentFollowedByAnExit()
    {
        return VerifyAsync("""
            class C
            {
                void M(List<int> items)
                {
                    foreach (var item in items)
                    {
                        items[0] = item;
                        break;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AddsLinqForACollectionWithoutAnInstanceToArray()
    {
        return CodeFixTestRunner.RunAsync<CollectionMutationInEnumerationAnalyzer, CollectionMutationInEnumerationCodeFixProvider>(
            """
            using System.Collections.Generic;

            class C
            {
                void M(ICollection<int> items)
                {
                    foreach (var item in {|E128155:items|})
                    {
                        items.Add(item);
                    }
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                void M(ICollection<int> items)
                {
                    foreach (var item in items.ToArray())
                    {
                        items.Add(item);
                    }
                }
            }
            """);
    }
}
