using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ListGetRangeCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<ListGetRangeAnalyzer, ListGetRangeCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesGetRangeWithAsSpanSlice()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                void M()
                {
                    var list = new List<int> { 1, 2, 3, 4, 5 };
                    var range = {|E128084:list.GetRange(1, 3)|};
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Runtime.InteropServices;

            class C
            {
                void M()
                {
                    var list = new List<int> { 1, 2, 3, 4, 5 };
                    var range = CollectionsMarshal.AsSpan(list).Slice(1, 3).ToArray();
                }
            }
            """);
    }
}
