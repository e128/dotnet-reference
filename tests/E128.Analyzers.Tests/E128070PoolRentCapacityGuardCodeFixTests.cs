using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128070PoolRentCapacityGuardCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<PoolRentCapacityGuardAnalyzer, PoolRentCapacityGuardCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    private static Task VerifyFixWithCapacityAsync(string capacity, string source, string fixedCode)
    {
        var test = new CSharpCodeFixTest<PoolRentCapacityGuardAnalyzer, PoolRentCapacityGuardCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", "is_global = true\ne128_pool_rent_max_capacity = " + capacity));
        return test.RunAsync();
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
