using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsDirectInstantiationAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FipsDirectInstantiationAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NameMatchedCipherInCryptographyNamespace_SkippedToAvoidDoubleReport()
    {
        return VerifyAsync("""
                           namespace System.Security.Cryptography;
                           class BlowfishManaged
                           {
                           }

                           class C
                           {
                               void M()
                               {
                                   var c = new BlowfishManaged();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnmappableConstructorArguments_FallBackToBaseFactoryMessage()
    {
        return VerifyAsync("""
                           namespace System.Security.Cryptography;
                           class CustomCng
                           {
                               public CustomCng(object key)
                               {
                               }
                           }

                           class C
                           {
                               void M(object key)
                               {
                                   var c = {|E128142:new CustomCng(key)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ManagedType_Reports_E128142()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var h = {|E128142:new SHA256Managed()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RijndaelCreate_Reports_E128142()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var r = {|E128142:Rijndael.Create()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AbstractFactory_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var h = SHA256.Create();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CngType_Reports_E128142()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var aes = {|E128142:new AesCng()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CngConstructorWithCngKey_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(CngKey key)
                               {
                                   using var rsa = new RSACng(key);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CngConstructorWithCngProvider_NoReport()
    {
        return VerifyAsync("""
                           namespace System.Security.Cryptography;
                           class CustomCng
                           {
                               public CustomCng(CngProvider provider)
                               {
                               }
                           }

                           class C
                           {
                               void M(CngProvider provider)
                               {
                                   var c = new CustomCng(provider);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RijndaelManaged_Reports_E128142()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var r = {|E128142:new RijndaelManaged()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TypeBlockedByE128071_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var h = new SHA1Managed();
                               }
                           }
                           """);
    }
}
