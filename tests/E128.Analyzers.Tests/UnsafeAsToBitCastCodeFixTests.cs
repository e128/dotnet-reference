using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeAsToBitCastCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<UnsafeAsToBitCastAnalyzer, UnsafeAsToBitCastCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheTargetIsAReferenceType()
    {
        return CodeFixTestRunner.NoFixAsync<UnsafeAsToBitCastAnalyzer, UnsafeAsToBitCastCodeFixProvider>("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               void M()
                               {
                                   int value = 1;
                                   object result = {|E128112:Unsafe.As<int, object>(ref value)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesAsWithBitCast()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    int value = 1;
                    float result = {|E128112:Unsafe.As<int, float>(ref value)|};
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    int value = 1;
                    float result = Unsafe.BitCast<int, float>(value);
                }
            }
            """);
    }
}
