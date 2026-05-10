using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InsecureRandomInCryptoContextAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<InsecureRandomInCryptoContextAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NewRandom_InCryptoContext()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var r = {|E128075:new Random()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RandomShared_InCryptoContext()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               int M() => {|E128075:Random.Shared|}.Next();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WithoutCryptoImport()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   var r = new Random();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenUsingRandomNumberGenerator()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var bytes = RandomNumberGenerator.GetBytes(16);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_BothOccurrences_InCryptoContext()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var r = {|E128075:new Random()|};
                                   var n = {|E128075:Random.Shared|}.Next();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_WhenCryptoNamespaceInComment()
    {
        return VerifyAsync("""
                           using System;
                           // System.Security.Cryptography is not actually imported
                           class C
                           {
                               void M()
                               {
                                   var r = new Random();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NewRandom_WithSeed_InCryptoContext()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var r = {|E128075:new Random(42)|};
                               }
                           }
                           """);
    }
}
