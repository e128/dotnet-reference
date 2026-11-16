using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AggressiveOptimizationBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<AggressiveOptimizationAnalyzer, AggressiveOptimizationCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_ForOtherAttributesAndAnAttributeWithoutArguments()
    {
        return AnalyzerTestRunner.VerifyAsync<AggressiveOptimizationAnalyzer>(
            """
            using System;
            using System.Runtime.CompilerServices;
            class C
            {
                [Obsolete("old")]
                void First()
                {
                }

                [MethodImpl]
                void Second()
                {
                }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheRightOptionWhenTheLeftOneIsRemoved()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                [{|E128116:MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)|}]
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
    public Task Fix_RemovesOnlyTheAttributeWhenTheListHoldsSeveral()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Runtime.CompilerServices;
            class C
            {
                [Obsolete, {|E128116:MethodImpl(MethodImplOptions.AggressiveOptimization)|}]
                void M()
                {
                }
            }
            """,
            """
            using System;
            using System.Runtime.CompilerServices;
            class C
            {
                [Obsolete]
                void M()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RemovesALaterAttributeList()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Runtime.CompilerServices;
            class C
            {
                [Obsolete]
                [{|E128116:MethodImpl(MethodImplOptions.AggressiveOptimization)|}]
                void M()
                {
                }
            }
            """,
            """
            using System;
            using System.Runtime.CompilerServices;
            class C
            {
                [Obsolete]
                void M()
                {
                }
            }
            """);
    }
}
