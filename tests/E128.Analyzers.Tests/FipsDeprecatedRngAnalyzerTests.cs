using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsDeprecatedRngAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task NewProvider_Reports_E128147()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsDeprecatedRngAnalyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var rng = {|E128147:new RNGCryptoServiceProvider()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StaticFactory_NoReport()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsDeprecatedRngAnalyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var rng = RandomNumberGenerator.Create();
                               }
                           }
                           """);
    }
}
