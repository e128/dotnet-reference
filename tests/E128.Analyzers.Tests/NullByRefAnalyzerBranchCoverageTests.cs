using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NullByRefAnalyzerBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<NullByRefAnalyzer>(code, options: new AnalyzerTestOptions { AllowUnsafe = true });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAsRefOfANullLiteral()
    {
        return VerifyAsync("""
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M()
                {
                    ref int value = ref {|E128127:Unsafe.AsRef<int>(null)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAsRefOfADefaultPointer()
    {
        return VerifyAsync("""
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M()
                {
                    ref int value = ref {|E128127:Unsafe.AsRef<int>(default(void*))|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAsRefOfAZeroPointerCastWithEachLiteralType()
    {
        return VerifyAsync("""
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M()
                {
                    ref int a = ref {|E128127:Unsafe.AsRef<int>((void*)0u)|};
                    ref int b = ref {|E128127:Unsafe.AsRef<int>((void*)0L)|};
                    ref int c = ref {|E128127:Unsafe.AsRef<int>((void*)0UL)|};
                    ref int d = ref {|E128127:Unsafe.AsRef<int>((void*)null)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForNullRefCalledThroughUsingStatic()
    {
        return VerifyAsync("""
            using static System.Runtime.CompilerServices.Unsafe;
            class C
            {
                void M()
                {
                    ref int value = ref {|E128127:NullRef<int>()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAsRefOfAPointerThatIsNotNull()
    {
        return VerifyAsync("""
            using System.Runtime.CompilerServices;
            unsafe class C
            {
                void M(void* pointer, int value)
                {
                    ref int a = ref Unsafe.AsRef<int>((void*)1);
                    ref int b = ref Unsafe.AsRef<int>(pointer);
                    ref int c = ref Unsafe.AsRef<int>(in value);
                    ref int d = ref Unsafe.AsRef<int>((void*)pointer);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForSameNamedMethodsOnOtherTypes()
    {
        return VerifyAsync("""
            static class Unsafe
            {
                public static unsafe ref T AsRef<T>(void* source) where T : unmanaged { return ref *(T*)source; }
                public static ref T NullRef<T>() { throw new System.InvalidOperationException(); }
            }

            unsafe class C
            {
                void M()
                {
                    ref int a = ref Unsafe.AsRef<int>(null);
                    ref int b = ref Unsafe.NullRef<int>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNullRefIsReturnedAsTheNotFoundSentinel()
    {
        return VerifyAsync("""
            using System.Runtime.CompilerServices;
            class C
            {
                ref int Find(bool found, ref int value)
                {
                    if (!found)
                    {
                        return ref Unsafe.NullRef<int>();
                    }

                    return ref value;
                }

                ref int Missing() => ref Unsafe.NullRef<int>();
            }
            """);
    }
}
