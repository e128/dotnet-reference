using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ListGetRangeCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<ListGetRangeAnalyzer, ListGetRangeCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesGetRangeWithAsSpanSlice()
    {
        return VerifyFixAsync(
            """
            #nullable enable
            using System.Collections.Generic;
            class C
            {
                void M()
                {
                    var list = new List<int> { 1, 2, 3, 4, 5 };
                    IReadOnlyList<int> range = {|E128084:list.GetRange(1, 3)|};
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
            """);
    }
}
