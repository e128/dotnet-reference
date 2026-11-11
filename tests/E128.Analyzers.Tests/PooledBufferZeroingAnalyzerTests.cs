using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PooledBufferZeroingAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenReturnIsOutsideFinally()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
                           using System.Buffers;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var buffer = ArrayPool<byte>.Shared.Rent(32);
                                   CryptographicOperations.ZeroMemory(buffer);
                                   {|E128110:ArrayPool<byte>.Shared.Return(buffer)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenFinallyHasNoZeroMemory()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
                           using System.Buffers;
                           class C
                           {
                               void M()
                               {
                                   var buffer = ArrayPool<byte>.Shared.Rent(32);
                                   try { }
                                   finally
                                   {
                                       {|E128110:ArrayPool<byte>.Shared.Return(buffer)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyClearArrayIsUsed()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
                           using System.Buffers;
                           class C
                           {
                               void M()
                               {
                                   var buffer = ArrayPool<byte>.Shared.Rent(32);
                                   try { }
                                   finally
                                   {
                                       {|E128110:ArrayPool<byte>.Shared.Return(buffer, clearArray: true)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenZeroMemoryCoversOnlyASlice()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
                           using System;
                           using System.Buffers;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(int used)
                               {
                                   var buffer = ArrayPool<byte>.Shared.Rent(32);
                                   try { }
                                   finally
                                   {
                                       CryptographicOperations.ZeroMemory(buffer.AsSpan(0, used));
                                       {|E128110:ArrayPool<byte>.Shared.Return(buffer)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenZeroMemoryFollowsReturn()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
                           using System.Buffers;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var buffer = ArrayPool<byte>.Shared.Rent(32);
                                   try { }
                                   finally
                                   {
                                       {|E128110:ArrayPool<byte>.Shared.Return(buffer)|};
                                       CryptographicOperations.ZeroMemory({|E128111:buffer|});
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenFinallyZeroesWholeArrayBeforeReturn()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
                           using System.Buffers;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var buffer = ArrayPool<byte>.Shared.Rent(32);
                                   try { }
                                   finally
                                   {
                                       CryptographicOperations.ZeroMemory(buffer);
                                       ArrayPool<byte>.Shared.Return(buffer);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenZeroMemoryUsesAsSpan()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
                           using System;
                           using System.Buffers;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var buffer = ArrayPool<byte>.Shared.Rent(32);
                                   try { }
                                   finally
                                   {
                                       CryptographicOperations.ZeroMemory(buffer.AsSpan());
                                       ArrayPool<byte>.Shared.Return(buffer);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenReturnIsOnANonPoolType()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
                           class Counter
                           {
                               public void Return(byte[] data) { }
                           }
                           class C
                           {
                               void M(Counter counter, byte[] data)
                               {
                                   counter.Return(data);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenZeroMemoryIsConditional()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
                           using System.Buffers;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(bool flag)
                               {
                                   var buffer = ArrayPool<byte>.Shared.Rent(32);
                                   try { }
                                   finally
                                   {
                                       if (flag)
                                       {
                                           CryptographicOperations.ZeroMemory(buffer);
                                       }

                                       {|E128110:ArrayPool<byte>.Shared.Return(buffer)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenBufferIsUsedAfterReturn()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
                           using System.Buffers;
                           using System.Security.Cryptography;
                           class C
                           {
                               int M()
                               {
                                   var buffer = ArrayPool<byte>.Shared.Rent(32);
                                   try { }
                                   finally
                                   {
                                       CryptographicOperations.ZeroMemory(buffer);
                                       ArrayPool<byte>.Shared.Return(buffer);
                                   }

                                   return {|E128111:buffer|}.Length;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenBufferIsReassignedAfterReturn()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
                           using System.Buffers;
                           using System.Security.Cryptography;
                           class C
                           {
                               int M()
                               {
                                   var buffer = ArrayPool<byte>.Shared.Rent(32);
                                   try { }
                                   finally
                                   {
                                       CryptographicOperations.ZeroMemory(buffer);
                                       ArrayPool<byte>.Shared.Return(buffer);
                                   }

                                   buffer = new byte[4];
                                   return buffer.Length;
                               }
                           }
                           """);
    }
}
