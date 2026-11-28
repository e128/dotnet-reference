using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128070PoolRentCapacityGuardCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<PoolRentCapacityGuardAnalyzer, PoolRentCapacityGuardCodeFixProvider>(source, fixedCode);
    }

    private static Task VerifyFixWithCapacityAsync(string capacity, string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<PoolRentCapacityGuardAnalyzer, PoolRentCapacityGuardCodeFixProvider>(
            source,
            fixedCode,
            options: new CodeFixTestOptions { AnalyzerConfig = ("/.editorconfig", "is_global = true\ne128_pool_rent_max_capacity = " + capacity) });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_WrapsArgumentInMathMin_WithDefaultCapacity()
    {
        return VerifyFixAsync(
            """
            using System.Buffers;
            class C
            {
                void M(int size)
                {
                    var pool = ArrayPool<char>.Shared;
                    var buf = pool.{|E128070:Rent(size)|};
                }
            }
            """,
            """
            using System;
            using System.Buffers;
            class C
            {
                void M(int size)
                {
                    var pool = ArrayPool<char>.Shared;
                    var buf = pool.Rent(Math.Min(size, 1_048_576));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_UsesConfiguredCapacity()
    {
        return VerifyFixWithCapacityAsync(
            "4096",
            """
            using System;
            using System.Buffers;
            class C
            {
                void M(int size)
                {
                    var pool = ArrayPool<char>.Shared;
                    var buf = pool.{|E128070:Rent(size)|};
                }
            }
            """,
            """
            using System;
            using System.Buffers;
            class C
            {
                void M(int size)
                {
                    var pool = ArrayPool<char>.Shared;
                    var buf = pool.Rent(Math.Min(size, 4_096));
                }
            }
            """);
    }
}
