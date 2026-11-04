using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsAesGcmTagSizeAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FipsAesGcmTagSizeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortTagCtor_Reports_E128149()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var aes = {|E128149:new AesGcm(new byte[16], 12)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FullTagCtor_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var aes = new AesGcm(new byte[16], 16);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortTagBuffer_Reports_E128149()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(AesGcm aes)
                               {
                                   {|E128149:aes.Encrypt(new byte[12], new byte[1], new byte[1], new byte[12])|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FullTagBuffer_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(AesGcm aes)
                               {
                                   aes.Encrypt(new byte[12], new byte[1], new byte[1], new byte[16]);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortTagDecrypt_Reports_E128149()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(AesGcm aes)
                               {
                                   {|E128149:aes.Decrypt(new byte[12], new byte[1], new byte[12], new byte[1])|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TagSizeInUnreassignedLocal_Reports_E128149()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var tagSize = 12;
                                   using var aes = {|E128149:new AesGcm(new byte[16], tagSize)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TagSizeInReadonlyField_Reports_E128149()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               private readonly int tagSize = 12;

                               void M()
                               {
                                   using var aes = {|E128149:new AesGcm(new byte[16], tagSize)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TagBufferInUnreassignedLocal_Reports_E128149()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(AesGcm aes)
                               {
                                   var tag = new byte[12];
                                   {|E128149:aes.Encrypt(new byte[12], new byte[1], new byte[1], tag)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TagBufferInReadonlyField_Reports_E128149()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               private readonly byte[] tag = new byte[12];

                               void M(AesGcm aes)
                               {
                                   {|E128149:aes.Encrypt(new byte[12], new byte[1], new byte[1], tag)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TagSizeInReassignedLocal_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var tagSize = 12;
                                   tagSize = 16;
                                   using var aes = new AesGcm(new byte[16], tagSize);
                               }
                           }
                           """);
    }
}
