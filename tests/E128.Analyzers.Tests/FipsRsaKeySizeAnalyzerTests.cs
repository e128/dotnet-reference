using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsRsaKeySizeAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FipsRsaKeySizeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortRsaKey_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var rsa = {|E128143:RSA.Create(2048)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StrongRsaKey_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var rsa = RSA.Create(3072);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortCurve_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var key = {|E128143:ECDsa.Create(ECCurve.NamedCurves.nistP256)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FactoryWithoutCurve_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var key = {|E128143:ECDsa.Create()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ApprovedCurve_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RsaCreatedByName_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var rsa = {|E128143:RSA.Create()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnresolvableCurveName_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(string name)
                               {
                                   using var key = {|E128143:ECDsa.Create(ECCurve.CreateFromFriendlyName(name))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CurveFromParameter_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(ECCurve curve)
                               {
                                   using var key = {|E128143:ECDsa.Create(curve)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortKeySizeAssignment_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(RSA rsa)
                               {
                                   {|E128143:rsa.KeySize = 2048|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CompoundKeySizeAssignment_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(RSA rsa)
                               {
                                   rsa.KeySize += 1;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortEllipticCurveKeySize_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var key = {|E128143:new ECDsaCng(256)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ApprovedP521Curve_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var key = ECDsa.Create(ECCurve.NamedCurves.nistP521);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RsaCreatedByName_ReportsUnverifiableKeySize_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var rsa = {|E128143:CryptoConfig.CreateFromName("RSA")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RsaImportedFromParameters_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(RSA rsa, RSAParameters parameters)
                               {
                                   {|E128143:rsa.ImportParameters(parameters)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RsaImportedFromHandle_Reports_E128143()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(IntPtr handle)
                               {
                                   using var rsa = {|E128143:new RSAOpenSsl(handle)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RsaImportedFromBlob_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(RSA rsa, byte[] blob)
                               {
                                   {|E128143:rsa.ImportRSAPrivateKey(blob, out _)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RsaImportedFromPem_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M(RSA rsa, string pem)
                               {
                                   {|E128143:rsa.ImportFromPem(pem)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RsaFromCertificate_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography.X509Certificates;
                           class C
                           {
                               void M(X509Certificate2 certificate)
                               {
                                   var rsa = {|E128143:certificate.GetRSAPublicKey()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CurveFromFriendlyName_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var key = {|E128143:ECDsa.Create(ECCurve.CreateFromFriendlyName("nistP256"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CurveFromObjectIdentifier_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var key = {|E128143:ECDsa.Create(ECCurve.CreateFromOid(new Oid("1.2.840.10045.3.1.7")))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CurveInStructInitializer_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var parameters = new ECParameters { {|E128143:Curve = ECCurve.NamedCurves.nistP256|} };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorWithoutCurve_Reports_E128143()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var key = {|E128143:new ECDsaCng()|};
                               }
                           }
                           """);
    }
}
