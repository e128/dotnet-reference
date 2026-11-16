using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PooledBufferZeroingBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAWholeIntArrayIsZeroedThroughAsBytes()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
            using System;
            using System.Buffers;
            using System.Runtime.InteropServices;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var buffer = ArrayPool<int>.Shared.Rent(8);
                    try { }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(buffer.AsSpan()));
                        ArrayPool<int>.Shared.Return(buffer);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnotherBufferIsZeroed()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
            using System.Buffers;
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] other)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    try { }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(other);
                        {|E128110:ArrayPool<byte>.Shared.Return(buffer)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAFunctionNamedZeroMemoryIsNotCryptographicOperations()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
            using System.Buffers;
            class C
            {
                static void ZeroMemory(byte[] buffer) { }
                void M()
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    try { }
                    finally
                    {
                        ZeroMemory(buffer);
                        {|E128110:ArrayPool<byte>.Shared.Return(buffer)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheBufferIsUsedAfterTheEnclosingBlock()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
            using System;
            using System.Buffers;
            class C
            {
                void M(bool flag)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    if (flag)
                    {
                        {|E128110:ArrayPool<byte>.Shared.Return(buffer)|};
                    }
                    Console.WriteLine({|E128111:buffer|}.Length);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForUseAfterANewBufferReplacesTheReturnedBuffer()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
            using System;
            using System.Buffers;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    try { }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(buffer);
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                    buffer = new byte[8];
                    Console.WriteLine(buffer.Length);
                }
            }
            """);
    }
}
