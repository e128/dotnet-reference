using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsCipherModeAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task EcbMember_Reports_E128144()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsCipherModeAnalyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(Aes aes)
                               {
                                   aes.Mode = {|E128144:CipherMode.ECB|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EncryptEcb_Reports_E128144()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsCipherModeAnalyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(Aes aes)
                               {
                                   {|E128144:aes.EncryptEcb(new byte[16], new byte[16], PaddingMode.None)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CbcMember_NoReport()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsCipherModeAnalyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(Aes aes)
                               {
                                   aes.Mode = CipherMode.CBC;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ModeComparison_NoReport()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsCipherModeAnalyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(Aes aes)
                               {
                                   if (aes.Mode == CipherMode.ECB)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ModeEqualsReceiver_NoReport()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsCipherModeAnalyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(Aes aes)
                               {
                                   if (CipherMode.ECB.Equals(aes.Mode))
                                   {
                                   }
                               }
                           }
                           """);
    }
}
