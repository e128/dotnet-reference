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

    [Fact]
    [Trait("Category", "CI")]
    public Task FixByteArray_BecomesReadOnlySpan()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static readonly {|E128061:byte[] Magic = [0x50, 0x4B, 0x03, 0x04]|};
            }
            """,
            """
            using System;

            class C
            {
                private static ReadOnlySpan<byte> Magic => [0x50, 0x4B, 0x03, 0x04];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixByteArray_WithNewInitializer_BecomesReadOnlySpan()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static readonly {|E128061:byte[] Mask = new byte[] { 0xFF, 0x0F }|};
            }
            """,
            """
            using System;

            class C
            {
                private static ReadOnlySpan<byte> Mask => [0xFF, 0x0F];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixByteArray_WithNonConstantElement_BecomesImmutableArray()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static readonly {|E128061:byte[] Bytes = new byte[] { 1, ReadOne() }|};

                private static byte ReadOne() => 1;
            }
            """,
            """
            using System.Collections.Immutable;

            class C
            {
                private static readonly ImmutableArray<byte> Bytes = [1, ReadOne()];

                private static byte ReadOne() => 1;
            }
            """);
    }
}
