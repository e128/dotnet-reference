using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers which Rent calls the capacity rule inspects and which guards it accepts.
/// </summary>
public sealed class PoolRentCapacityGuardBranchCoverageTests
{
    private const string Pools = """
                                 using System;
                                 using System.Buffers;
                                 class BytePool
                                 {
                                     public byte[] Rent(int size) => new byte[size];
                                 }

                                 class lowerpool
                                 {
                                     public byte[] Rent(int size) => new byte[size];
                                 }

                                 class Allocator
                                 {
                                     public byte[] Rent(int size) => new byte[size];
                                 }

                                 class WidePool
                                 {
                                     public byte[] Rent(long size) => new byte[size];
                                     public byte[] Rent(int size, int extra = 0) => new byte[size];
                                 }

                                 static class Math2
                                 {
                                     public static int Min(int a, int b) => a < b ? a : b;
                                 }

                                 """;

    private static Task VerifyAsync(string body, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<PoolRentCapacityGuardAnalyzer>(
            Pools + "class C\n{\n" + body + "\n}\n",
            expected: null,
            options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAMemoryPoolRentOfAVariableSize()
    {
        return VerifyAsync("""
                               void M(int size)
                               {
                                   using var owner = MemoryPool<byte>.Shared.{|E128070:Rent(size)|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForATypeWhoseNameEndsInPool()
    {
        return VerifyAsync("""
                               void M(BytePool pool, int size)
                               {
                                   var buffer = pool.{|E128070:Rent(size)|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForATypeWhoseNameContainsLowercasePool()
    {
        return VerifyAsync("""
                               void M(lowerpool pool, int size)
                               {
                                   var buffer = pool.{|E128070:Rent(size)|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForANegativeConstantSize()
    {
        return VerifyAsync("""
                               void M(BytePool pool)
                               {
                                   var buffer = pool.{|E128070:Rent(-1)|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheArgumentIsAnotherCall()
    {
        return VerifyAsync("""
                               static int Size() => 10;

                               void M(BytePool pool)
                               {
                                   var buffer = pool.{|E128070:Rent(Size())|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCapComesFromAMinLookAlike()
    {
        return VerifyAsync("""
                               void M(BytePool pool, int size)
                               {
                                   var buffer = pool.{|E128070:Rent(Math2.Min(size, 10))|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsALocalFromMathMin()
    {
        return VerifyAsync("""
                               void M(BytePool pool, int size)
                               {
                                   var capped = Math.Min(size, 1024);
                                   var buffer = pool.Rent(capped);
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLocalComesFromAnotherCall()
    {
        return VerifyAsync("""
                               void M(BytePool pool, int size)
                               {
                                   var wider = Math.Max(size, 1024);
                                   var buffer = pool.{|E128070:Rent(wider)|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLocalComesFromAMinLookAlike()
    {
        return VerifyAsync("""
                               void M(BytePool pool, int size)
                               {
                                   var capped = Math2.Min(size, 1024);
                                   var buffer = pool.{|E128070:Rent(capped)|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLocalComesFromAPlainValue()
    {
        return VerifyAsync("""
                               void M(BytePool pool, int size)
                               {
                                   var copy = size;
                                   var buffer = pool.{|E128070:Rent(copy)|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLocalHasNoInitializer()
    {
        return VerifyAsync("""
                               void M(BytePool pool, int size)
                               {
                                   int later;
                                   later = size;
                                   var buffer = pool.{|E128070:Rent(later)|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheArgumentIsAField()
    {
        return VerifyAsync("""
                               private int _size;

                               void M(BytePool pool)
                               {
                                   var buffer = pool.{|E128070:Rent(_size)|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsNotAPool()
    {
        return VerifyAsync("""
                               void M(Allocator allocator, int size)
                               {
                                   var buffer = allocator.Rent(size);
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRentTakesALongSize()
    {
        return VerifyAsync("""
                               void M(WidePool pool, long size)
                               {
                                   var buffer = pool.Rent(size);
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRentOverloadHasASecondParameter()
    {
        return VerifyAsync("""
                               void M(WidePool pool, int size)
                               {
                                   var buffer = pool.Rent(size);
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsNotAMemberCall()
    {
        return VerifyAsync("""
                               byte[] Rent(int size) => new byte[size];

                               void M(int size)
                               {
                                   var buffer = Rent(size);
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMethodIsNotNamedRent()
    {
        return VerifyAsync("""
                               void M(BytePool pool)
                               {
                                   var text = pool.ToString();
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallDoesNotBind()
    {
        return VerifyAsync(
            """
                void M(BytePool pool)
                {
                    var buffer = pool.Rent(missing);
                }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}
