using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class WeakSymmetricKeySourceAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheKeyComesFromAtextEncoding()
    {
        return AnalyzerTestRunner.VerifyAsync<WeakSymmetricKeySourceAnalyzer>("""
                           using System.Security.Cryptography;
                           using System.Text;

                           class C
                           {
                               void Run(Aes aes, string password)
                               {
                                   [|aes.Key = Encoding.UTF8.GetBytes(password)|];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeyComesFromARandomGenerator()
    {
        return AnalyzerTestRunner.VerifyAsync<WeakSymmetricKeySourceAnalyzer>("""
                           using System.Security.Cryptography;

                           class C
                           {
                               void Run(Aes aes)
                               {
                                   aes.Key = RandomNumberGenerator.GetBytes(32);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeyComesFromAKeyDerivation()
    {
        return AnalyzerTestRunner.VerifyAsync<WeakSymmetricKeySourceAnalyzer>("""
                           using System.Security.Cryptography;

                           class C
                           {
                               void Run(Aes aes, string password, byte[] salt)
                               {
                                   aes.Key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
                               }
                           }
                           """);
    }
}
