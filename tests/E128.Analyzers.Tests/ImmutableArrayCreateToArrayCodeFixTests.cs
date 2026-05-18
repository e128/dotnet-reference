using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ImmutableArrayCreateToArrayCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<ImmutableArrayCreateToArrayAnalyzer, ImmutableArrayCreateToArrayCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesCreateWithAsImmutableArray()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            using System.Collections.Immutable;
            class C
            {
                void M()
                {
                    var list = new List<int> { 1, 2, 3 };
                    var arr = {|E128083:ImmutableArray.Create(list.ToArray())|};
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Runtime.InteropServices;

            class C
            {
                void M()
                {
                    var list = new List<int> { 1, 2, 3 };
                    var arr = ImmutableCollectionsMarshal.AsImmutableArray(list.ToArray());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesCreateRangeWithAsImmutableArray()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            using System.Collections.Immutable;
            class C
            {
                void M()
                {
                    var list = new List<int> { 1, 2, 3 };
                    var arr = {|E128083:ImmutableArray.CreateRange(list.ToArray())|};
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Runtime.InteropServices;

            class C
            {
                void M()
                {
                    var list = new List<int> { 1, 2, 3 };
                    var arr = ImmutableCollectionsMarshal.AsImmutableArray(list.ToArray());
                }
            }
            """);
    }
}
