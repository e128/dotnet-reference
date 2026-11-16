using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AggressiveOptimizationCodeFixProviderBranchCoverageMoreTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<AggressiveOptimizationAnalyzer, AggressiveOptimizationCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsBothRemainingOptionsOfANestedCombination()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                [{|E128116:MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)|}]
                void M()
                {
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.NoInlining)]
                void M()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_DropsAnArgumentThatHoldsOnlyTheOptionTwice()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                [{|E128116:MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveOptimization, MethodCodeType = MethodCodeType.Native)|}]
                void M()
                {
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                [MethodImpl(MethodCodeType = MethodCodeType.Native)]
                void M()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RemovesTheAttributeListOfALocalFunction()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    [{|E128116:MethodImpl(MethodImplOptions.AggressiveOptimization)|}]
                    void Local()
                    {
                    }

                    Local();
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    void Local()
                    {
                    }

                    Local();
                }
            }
            """);
    }
}
