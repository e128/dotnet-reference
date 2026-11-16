using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LinearLookupInLoopCodeFixBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<LinearLookupInLoopAnalyzer, LinearLookupInLoopCodeFixProvider>(source, fixedCode);
    }

    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<LinearLookupInLoopAnalyzer, LinearLookupInLoopCodeFixProvider>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_HoistsTheSetAboveAWhileLoop()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<int> items, List<int> lookup)
                {
                    var i = 0;
                    while (i < items.Count)
                    {
                        if ({|E128066:lookup.Contains(items[i])|}) { }
                        i++;
                    }
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<int> items, List<int> lookup)
                {
                    var i = 0;
                    var lookupSet = lookup.ToHashSet();
                    while (i < items.Count)
                    {
                        if (lookupSet.Contains(items[i])) { }
                        i++;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_HoistsTheSetAboveTheLoopWhenALambdaDoesTheLookup()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<int> items, List<int> lookup)
                {
                    foreach (var item in items)
                    {
                        Func<bool> check = () => {|E128066:lookup.Contains(item)|};
                    }
                }
            }
            """,
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<int> items, List<int> lookup)
                {
                    var lookupSet = lookup.ToHashSet();
                    foreach (var item in items)
                    {
                        Func<bool> check = () => lookupSet.Contains(item);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_ForMethodsOtherThanContainsAndAny()
    {
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<int> items, List<int> lookup)
                {
                    foreach (var item in items)
                    {
                        _ = {|E128066:lookup.Remove(item)|};
                        _ = {|E128066:lookup.Find(x => x == item)|};
                        _ = {|E128066:lookup.Where(x => x == item).Count()|};
                    }
                }
            }
            """);
    }
}
