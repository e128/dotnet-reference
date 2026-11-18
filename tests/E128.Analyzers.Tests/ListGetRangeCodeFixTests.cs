using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ListGetRangeCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesGetRangeWithAsSpanSlice()
    {
        return CodeFixTestRunner.RunAsync<ListGetRangeAnalyzer, ListGetRangeCodeFixProvider>(
            """
            #nullable enable
            using System.Collections.Generic;
            class C
            {
                void M()
                {
                    var list = new List<int> { 1, 2, 3, 4, 5 };
                    IReadOnlyList<int> range = {|#0:list.GetRange(1, 3)|};
                }
            }
            """,
            """
            #nullable enable
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                void M()
                {
                    var list = new List<int> { 1, 2, 3, 4, 5 };
                    IReadOnlyList<int> range = CollectionsMarshal.AsSpan(list).Slice(1, 3).ToArray();
                }
            }
            """,
            expected: [new DiagnosticResult("E128084", DiagnosticSeverity.Warning).WithLocation(0)]);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesTheIndexerLoopWithAsSpan()
    {
        return CodeFixTestRunner.RunAsync<ListGetRangeAnalyzer, ListGetRangeCodeFixProvider>(
            """
            using System.Collections.Generic;

            class C
            {
                int Total(List<string> items)
                {
                    int total = 0;
                    {|#0:for (int i = 0; i < items.Count; i++)
                    {
                        total += items[i].Length;
                    }|}
                    return total;
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                int Total(List<string> items)
                {
                    int total = 0;
                    foreach (var i in CollectionsMarshal.AsSpan(items))
                    {
                        total += i.Length;
                    }
                    return total;
                }
            }
            """,
            expected: [new DiagnosticResult("E128084", DiagnosticSeverity.Warning).WithLocation(0)]);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_IndexerLoop_KeepsExistingInteropUsing()
    {
        return CodeFixTestRunner.RunAsync<ListGetRangeAnalyzer, ListGetRangeCodeFixProvider>(
            """
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                int Total(List<string> items)
                {
                    int total = 0;
                    {|#0:for (int i = 0; i < items.Count; i++)
                    {
                        total += items[i].Length;
                    }|}
                    return total;
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                int Total(List<string> items)
                {
                    int total = 0;
                    foreach (var i in CollectionsMarshal.AsSpan(items))
                    {
                        total += i.Length;
                    }
                    return total;
                }
            }
            """,
            expected: [new DiagnosticResult("E128084", DiagnosticSeverity.Warning).WithLocation(0)]);
    }
}
