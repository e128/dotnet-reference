using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PooledBufferZeroingCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<PooledBufferZeroingAnalyzer, PooledBufferZeroingCodeFixProvider>(source, fixedCode);

    private static Task VerifyNoFixAsync(string code) =>
        CodeFixTestRunner.NoFixAsync<PooledBufferZeroingAnalyzer, PooledBufferZeroingCodeFixProvider>(code);

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
