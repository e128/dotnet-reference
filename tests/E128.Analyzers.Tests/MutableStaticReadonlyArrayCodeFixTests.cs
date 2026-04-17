using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MutableStaticReadonlyArrayCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<MutableStaticReadonlyArrayAnalyzer, MutableStaticReadonlyArrayCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixArray_TransformsToImmutableArray()
    {
        return VerifyFixAsync(
            """
            using System;
            class C
            {
                private static readonly {|E128061:string[] Names = ["a", "b"]|};
            }
            """,
            """
            using System;
            using System.Collections.Immutable;

            class C
            {
                private static readonly ImmutableArray<string> Names = ["a", "b"];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixArray_PreservesExistingUsing()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Collections.Immutable;
            class C
            {
                private static readonly {|E128061:string[] Names = ["a", "b"]|};
            }
            """,
            """
            using System;
            using System.Collections.Immutable;
            class C
            {
                private static readonly ImmutableArray<string> Names = ["a", "b"];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixArray_IntType()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static readonly {|E128061:int[] Ports = [80, 443]|};
            }
            """,
            """
            using System.Collections.Immutable;

            class C
            {
                private static readonly ImmutableArray<int> Ports = [80, 443];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixArray_WithNewArrayInitializer()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static readonly {|E128061:string[] Suffixes = new[] { "s_", "m_" }|};
            }
            """,
            """
            using System.Collections.Immutable;

            class C
            {
                private static readonly ImmutableArray<string> Suffixes = ["s_", "m_"];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixArray_WithExplicitTypeInNew()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static readonly {|E128061:string[] Suffixes = new string[] { "s_", "m_" }|};
            }
            """,
            """
            using System.Collections.Immutable;

            class C
            {
                private static readonly ImmutableArray<string> Suffixes = ["s_", "m_"];
            }
            """);
    }
}
