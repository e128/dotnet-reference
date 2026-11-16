using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MutableStaticReadonlyArrayCodeFixProviderBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<MutableStaticReadonlyArrayAnalyzer, MutableStaticReadonlyArrayCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ConvertsAnImplicitByteArrayOfByteCasts()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static readonly {|E128061:byte[] Magic = new[] { (byte)1, (byte)2 }|};
            }
            """,
            """
            using System;

            class C
            {
                private static ReadOnlySpan<byte> Magic => [(byte)1, (byte)2];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AcceptsNamedByteConstants()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private const byte Tag = 7;
                private static readonly {|E128061:byte[] Header = [Tag, 255]|};
            }
            """,
            """
            using System;

            class C
            {
                private const byte Tag = 7;
                private static ReadOnlySpan<byte> Header => [Tag, 255];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_FallsBackToImmutableArrayForASpreadElement()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static readonly System.Collections.Generic.List<byte> Prefix = new();
                private static readonly {|E128061:byte[] Full = [.. Prefix, 2]|};
            }
            """,
            """
            using System.Collections.Immutable;

            class C
            {
                private static readonly System.Collections.Generic.List<byte> Prefix = new();
                private static readonly ImmutableArray<byte> Full = [.. Prefix, 2];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_FallsBackToImmutableArrayForAConstantsFollowedByACall()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static readonly {|E128061:byte[] Values = [1, 2, ReadOne()]|};

                private static byte ReadOne() => 1;
            }
            """,
            """
            using System.Collections.Immutable;

            class C
            {
                private static readonly ImmutableArray<byte> Values = [1, 2, ReadOne()];

                private static byte ReadOne() => 1;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ConvertsAnImplicitArrayOfStrings()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static readonly {|E128061:string[] Names = new[] { "a", "b" }|};
            }
            """,
            """
            using System.Collections.Immutable;

            class C
            {
                private static readonly ImmutableArray<string> Names = ["a", "b"];
            }
            """);
    }
}
