using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CertificateChainBypassAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheBuildResultIsDiscarded()
    {
        return AnalyzerTestRunner.VerifyAsync<CertificateChainBypassAnalyzer>("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               void Run(X509Chain chain, X509Certificate2 certificate)
                               {
                                   [|chain.Build(certificate)|];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBuildResultIsChecked()
    {
        return AnalyzerTestRunner.VerifyAsync<CertificateChainBypassAnalyzer>("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               bool Run(X509Chain chain, X509Certificate2 certificate)
                               {
                                   return chain.Build(certificate);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePolicyAllowsAnUnknownAuthority()
    {
        return AnalyzerTestRunner.VerifyAsync<CertificateChainBypassAnalyzer>("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               void Run(X509ChainPolicy policy)
                               {
                                   [|policy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority|];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePolicyDisablesRevocation()
    {
        return AnalyzerTestRunner.VerifyAsync<CertificateChainBypassAnalyzer>("""
                           using System.Security.Cryptography.X509Certificates;

                           class C
                           {
                               void Run(X509ChainPolicy policy)
                               {
                                   [|policy.RevocationMode = X509RevocationMode.NoCheck|];
                               }
                           }
                           """);
    }
}
