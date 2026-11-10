using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsDeprecatedRngAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FipsDeprecatedRngAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NewProvider_Reports_E128147()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
