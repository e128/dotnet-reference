using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsHmacKeyLengthAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FipsHmacKeyLengthAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortKeyCtor_Reports_E128151()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var hmac = {|E128151:new HMACSHA256(new byte[16])|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortInitializerArrayKey_Reports_E128151()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var hmac = {|E128151:new HMACSHA256(new byte[] { 0x01, 0x02, 0x03, 0x04 })|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FullKeyCtor_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var hmac = new HMACSHA256(new byte[32]);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortStaticKey_Reports_E128151()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var mac = {|E128151:HMACSHA512.HashData(new byte[16], new byte[1])|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FullStaticKey_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var mac = HMACSHA512.HashData(new byte[64], new byte[1]);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EncodedStringKey_Reports_E128151()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           using System.Text;
                           class C
                           {
                               void M()
                               {
                                   using var hmac = {|E128151:new HMACSHA256(Encoding.UTF8.GetBytes("short"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HexStringKey_Reports_E128151()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var hmac = {|E128151:new HMACSHA256(Convert.FromHexString("00112233445566778899AABBCCDDEEFF"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortSha384Key_Reports_E128151()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var hmac = {|E128151:new HMACSHA384(new byte[32])|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FullSha384Key_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var hmac = new HMACSHA384(new byte[48]);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortTryHashData_Reports_E128151()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(byte[] destination)
                               {
                                   {|E128151:HMACSHA256.TryHashData(new byte[16], new byte[1], destination, out var written)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortHashDataAsync_Reports_E128151()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(Stream source)
                               {
                                   var mac = {|E128151:HMACSHA256.HashDataAsync(new byte[16], source)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortKeyPropertyAssignment_Reports_E128151()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var hmac = new HMACSHA256();
                                   {|E128151:hmac.Key = new byte[16]|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FullKeyPropertyAssignment_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var hmac = new HMACSHA256();
                                   hmac.Key = new byte[32];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonConstantKeyLength_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(byte[] key)
                               {
                                   using var hmac = new HMACSHA256(key);
                               }
                           }
                           """);
    }
}
