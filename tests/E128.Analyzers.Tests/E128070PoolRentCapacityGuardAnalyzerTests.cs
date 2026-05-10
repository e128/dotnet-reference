using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128070PoolRentCapacityGuardAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<PoolRentCapacityGuardAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Rent_IntMaxValue_Literal_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               void M()
                               {
                                   var pool = ArrayPool<char>.Shared;
                                   var buf = pool.{|E128070:Rent(int.MaxValue)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Rent_IntMaxValue_Constant_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               const int Size = int.MaxValue;
                               void M()
                               {
                                   var pool = ArrayPool<char>.Shared;
                                   var buf = pool.{|E128070:Rent(Size)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Rent_Variable_NoGuard_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               void M(int capacity)
                               {
                                   var pool = ArrayPool<char>.Shared;
                                   var buf = pool.{|E128070:Rent(capacity)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Rent_MathMin_Guard_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               void M(int capacity)
                               {
                                   var pool = ArrayPool<char>.Shared;
                                   var buf = pool.Rent(Math.Min(capacity, 1024));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Rent_MathMin_ReverseArgs_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               void M(int capacity)
                               {
                                   var pool = ArrayPool<char>.Shared;
                                   var buf = pool.Rent(Math.Min(1024, capacity));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Rent_Literal_SmallValue_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               void M()
                               {
                                   var pool = ArrayPool<char>.Shared;
                                   var buf = pool.Rent(1024);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Rent_NoArgs_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               void M()
                               {
                                   var pool = ArrayPool<char>.Shared;
                                   var buf = pool.Rent(0);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Rent_GuardedVariable_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               void M(int capacity)
                               {
                                   var capped = Math.Min(capacity, 4096);
                                   var pool = ArrayPool<char>.Shared;
                                   var buf = pool.Rent(capped);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Rent_StaticMethod_IntMaxValue_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               void M()
                               {
                                   var buf = ArrayPool<byte>.Shared.{|E128070:Rent(int.MaxValue)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Rent_CustomPoolType_Fires()
    {
        return VerifyAsync("""
                           using System;
                           namespace Microsoft.Extensions.ObjectPool
                           {
                               public class ObjectPool<T> where T : class
                               {
                                   public static ObjectPool<T> Shared { get; } = new();
                                   public T Rent(int capacity) => default!;
                                   public void Return(T obj) { }
                               }
                           }
                           namespace App
                           {
                               using Microsoft.Extensions.ObjectPool;
                               using System.Text;
                               class StringBuilderPool
                               {
                                   public static StringBuilderPool Shared { get; } = new();
                                   public StringBuilder Rent(int capacity) => new(capacity);
                                   public void Return(StringBuilder sb) { }
                               }
                               class C
                               {
                                   void M(int size)
                                   {
                                       var sb = StringBuilderPool.Shared.{|E128070:Rent(size)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Rent_ConstantBelowThreshold_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               const int BufferSize = 8192;
                               void M()
                               {
                                   var buf = ArrayPool<byte>.Shared.Rent(BufferSize);
                               }
                           }
                           """);
    }
}
