using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PooledBufferZeroingCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<PooledBufferZeroingAnalyzer, PooledBufferZeroingCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    private static Task VerifyNoFixAsync(string code)
    {
        return new CSharpCodeFixTest<PooledBufferZeroingAnalyzer, PooledBufferZeroingCodeFixProvider, DefaultVerifier>
        {
            TestCode = code,
            FixedCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 0
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_InsertsZeroMemoryBeforeReturn()
    {
        return VerifyFixAsync(
            """
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
            """,
            """
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
    public Task CodeFix_ReusesExistingUsing_WhenCryptographyAlreadyImported()
    {
        return VerifyFixAsync(
            """
            using System.Buffers;
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] rented)
                {
                    var pool = ArrayPool<byte>.Create();
                    try { }
                    finally
                    {
                        {|E128110:pool.Return(rented)|};
                    }
                }
            }
            """,
            """
            using System.Buffers;
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] rented)
                {
                    var pool = ArrayPool<byte>.Create();
                    try { }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(rented);
                        pool.Return(rented);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenReturnHasNoEnclosingFinally()
    {
        // Zeroing outside a finally does not clear the buffer on every exit path, so the fix
        // would not satisfy the rule.
        return VerifyNoFixAsync("""
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
}
