using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PooledBufferZeroingAnalyzerBranchCoverageGapTests
{
    private static readonly AnalyzerTestOptions NoCompilerDiagnostics = new() { CompilerDiagnostics = CompilerDiagnostics.None };

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForReturnCallsWithAnUnexpectedArgumentCount()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
            class Pool
            {
                public void Return() { }

                public void Return(byte[] buffer, bool clear, int extra) { }
            }

            class C
            {
                void M(Pool pool, byte[] buffer)
                {
                    pool.Return();
                    pool.Return(buffer, true, 1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAReturnMethodOfAnotherTypeOrAnUnboundReceiver()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>(
            """
            class Pool
            {
                public void Return(byte[] buffer) { }
            }

            class C
            {
                void M(Pool pool, byte[] buffer)
                {
                    pool.Return(buffer);
                    missing.Return(buffer);
                }
            }
            """,
            expected: null,
            NoCompilerDiagnostics);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_UseAfterReturnBehindAStatementThatIgnoresTheBuffer()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>("""
            using System;
            using System.Buffers;
            class C
            {
                void M()
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    {|E128110:ArrayPool<byte>.Shared.Return(buffer)|};
                    Console.WriteLine("done");
                    Console.WriteLine({|E128111:buffer|}.Length);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFinallyBlockHoldsOnlyOtherCallsBeforeTheReturn()
    {
        return AnalyzerTestRunner.VerifyAsync<PooledBufferZeroingAnalyzer>(
            """
            using System;
            using System.Buffers;
            class C
            {
                void M()
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(8);
                    try { }
                    finally
                    {
                        Array.Clear(buffer);
                        Array.Clear(buffer, 0, buffer.Length);
                        Missing(buffer);
                        {|E128110:ArrayPool<byte>.Shared.Return(buffer)|};
                    }
                }
            }
            """,
            expected: null,
            NoCompilerDiagnostics);
    }
}
