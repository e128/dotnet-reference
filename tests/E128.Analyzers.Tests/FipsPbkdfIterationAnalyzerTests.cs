using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsPbkdfIterationAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FipsPbkdfIterationAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LowIterationCount_Reports_E128145()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var k = {|E128145:new Rfc2898DeriveBytes("pw", new byte[16], 1000, HashAlgorithmName.SHA256)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MinimumIterationCount_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var k = new Rfc2898DeriveBytes("pw", new byte[16], 600_000, HashAlgorithmName.SHA256);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WeakPrf_Reports_E128145()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var k = {|E128145:new Rfc2898DeriveBytes("pw", new byte[16], 600_000, HashAlgorithmName.SHA1)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StaticPbkdf2LowCount_Reports_E128145()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var k = {|E128145:Rfc2898DeriveBytes.Pbkdf2("pw", new byte[16], 1000, HashAlgorithmName.SHA256, 32)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StaticPbkdf2Strong_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var k = Rfc2898DeriveBytes.Pbkdf2("pw", new byte[16], 600_000, HashAlgorithmName.SHA256, 32);
                               }
                           }
                           """);
    }
}
