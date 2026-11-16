using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MutableStaticReadonlyArrayCodeFixProviderBranchCoverageGapTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<MutableStaticReadonlyArrayAnalyzer, MutableStaticReadonlyArrayCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_SplitsAMultiVariableByteFieldIntoOnePropertyEach()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static readonly {|E128061:{|E128061:byte[] First = [1, 2], Second = [3]|}|};
            }
            """,
            """
            using System;

            class C
            {
                private static ReadOnlySpan<byte> First => [1, 2];

                private static ReadOnlySpan<byte> Second => [3];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_LeavesAnUninitializedSiblingVariableAlone()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private static readonly {|E128061:string[] Names = ["a"], Spare|};
            }
            """,
            """
            using System.Collections.Immutable;

            class C
            {
                private static readonly ImmutableArray<string> Names = ["a"], Spare;
            }
            """);
    }
}
