using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AggressiveOptimizationCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<
            AggressiveOptimizationAnalyzer,
            AggressiveOptimizationCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RemovesAStandaloneAttribute()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                [{|E128116:MethodImpl(MethodImplOptions.AggressiveOptimization)|}]
                void M()
                {
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheRemainingOption()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                [{|E128116:MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)|}]
                void M()
                {
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void M()
                {
                }
            }
            """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheDocComment()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                /// <summary>Runs.</summary>
                [{|E128116:MethodImpl(MethodImplOptions.AggressiveOptimization)|}]
                void M()
                {
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                /// <summary>Runs.</summary>
                void M()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RemovesAParenthesizedOption()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                [{|E128116:MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions.AggressiveOptimization))|}]
                void M()
                {
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void M()
                {
                }
            }
            """);
    }
}
