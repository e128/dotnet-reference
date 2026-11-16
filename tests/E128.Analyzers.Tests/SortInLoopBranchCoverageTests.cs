using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SortInLoopBranchCoverageTests
{
    private static Task VerifyAsync(string code, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<SortInLoopAnalyzer>(code, expected: null, options: options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_ForEveryThenByStageInALoop()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<int> items, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        _ = {|E128068:{|E128068:items.OrderBy(x => x)|}.ThenBy(x => x)|};
                        _ = {|E128068:{|E128068:items.OrderByDescending(x => x)|}.ThenByDescending(x => x)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForSortMethodsThatAreNotEnumerableOrListOrArraySorts()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Linq;

            class Custom
            {
                public void Sort()
                {
                }

                public Custom OrderBy(System.Func<int, int> key) => this;
            }

            class C
            {
                void M(Custom custom, IQueryable<int> query, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        custom.Sort();
                        _ = custom.OrderBy(x => x);
                        _ = query.OrderBy(x => x);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSortCallDoesNotBind()
    {
        return VerifyAsync(
            """
            using System.Collections.Generic;
            class C
            {
                void M(List<int> items, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        items.Sort(1, 2, 3, 4, 5);
                    }
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenInsertOrRemoveAtZeroTargetsAnotherType()
    {
        return VerifyAsync("""
            using System.Text;
            class C
            {
                void M(StringBuilder builder, string text, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        _ = builder.Insert(0, "x");
                        _ = builder.Remove(0, 1);
                        _ = text.Insert(0, "x");
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheShiftIndexIsNotTheLiteralZero()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            class C
            {
                const int Zero = 0;

                void M(List<int> items, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        items.Insert(Zero, id);
                        items.Insert(id, id);
                        items.RemoveAt(Zero);
                        items.RemoveAt(id);
                        items.RemoveAt(0L > 1 ? 1 : 2);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenInsertOrRemoveAtHasNoArguments()
    {
        return VerifyAsync(
            """
            using System.Collections.Generic;
            class C
            {
                void M(List<int> items, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        items.Insert();
                        items.RemoveAt();
                    }
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallHasNoReceiver()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            class Bag : List<int>
            {
                void Run(int[] ids)
                {
                    foreach (var id in ids)
                    {
                        Sort();
                        Insert(0, id);
                    }
                }
            }
            """);
    }
}
