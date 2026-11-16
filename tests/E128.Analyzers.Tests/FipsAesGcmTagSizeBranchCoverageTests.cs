using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsAesGcmTagSizeBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsAesGcmTagSizeAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ShortTagBuffersAndConstructorTagSizes()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] key, byte[] nonce, byte[] plain, byte[] cipher, byte[] tag, int size)
                {
                    var a = {|E128149:new AesGcm(key, 12)|};
                    var b = new AesGcm(key, 16);
                    var c = new AesGcm(key, size);
                    a.Encrypt(nonce, plain, cipher, tag);
                    {|E128149:a.Encrypt(nonce, plain, cipher, new byte[12])|};
                    {|E128149:a.Decrypt(nonce, cipher, new byte[8], plain)|};
                    a.Encrypt(nonce, plain, cipher, new byte[16]);
                    a.Decrypt(nonce, cipher, tag, plain);
                    a.Dispose();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenEncryptOrDecryptBelongsToAnotherType()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class Own
            {
                public void Encrypt(byte[] tag) { }
            }

            class C
            {
                void M(RSA rsa, byte[] data, Own own)
                {
                    var a = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
                    own.Encrypt(new byte[4]);
                }
            }
            """);
    }
}
