using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NullByRefAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code, bool lenient = false)
    {
        return AnalyzerTestRunner.VerifyAsync<NullByRefAnalyzer>(
            code,
            options: new AnalyzerTestOptions
            {
                AllowUnsafe = true,
                CompilerDiagnostics = lenient ? CompilerDiagnostics.None : null,
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCalleeIsAnElementAccess()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                void M(Action[] callbacks)
                {
                    callbacks[0]();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenNullRefIsCopiedOutsideARefExpression()
    {
        return VerifyAsync("""
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    int copy = {|E128127:Unsafe.NullRef<int>()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheNullRefCallDoesNotBind()
    {
        return VerifyAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    ref int value = ref Unsafe.NullRef<int>(1);
                }
            }
            """,
            lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAsRefTakesNoArgumentOrTwoArguments()
    {
        return VerifyAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M(int value)
                {
                    ref int a = ref Unsafe.AsRef<int>();
                    ref int b = ref Unsafe.AsRef<int>(value, value);
                }
            }
            """,
            lenient: true);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAsRefOfTheDefaultLiteral()
    {
        return VerifyAsync(
            """
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M()
                {
                    ref int value = ref {|E128127:Unsafe.AsRef<int>(default)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAsRefOfAZeroIntegerPointerCast()
    {
        return VerifyAsync("""
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M()
                {
                    ref int value = ref {|E128127:Unsafe.AsRef<int>((void*)0)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAsRefOfNonZeroPointerCastsOfEachIntegerLiteralType()
    {
        return VerifyAsync("""
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M()
                {
                    ref int a = ref Unsafe.AsRef<int>((void*)1);
                    ref int b = ref Unsafe.AsRef<int>((void*)1u);
                    ref int c = ref Unsafe.AsRef<int>((void*)1L);
                    ref int d = ref Unsafe.AsRef<int>((void*)1UL);
                }
            }
            """);
    }
}
