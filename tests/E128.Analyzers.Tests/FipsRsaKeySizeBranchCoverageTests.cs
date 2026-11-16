using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsRsaKeySizeBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsRsaKeySizeAnalyzer>(code);
    }

    private static Task VerifyMessageAsync(string code, string message)
    {
        var expected = new DiagnosticResult("E128143", DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments(message);

        return AnalyzerTestRunner.VerifyAsync<FipsRsaKeySizeAnalyzer>(code, expected);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_DefaultRsaKeyOfTheFactory()
    {
        return VerifyMessageAsync(
            """
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    using var rsa = {|#0:RSA.Create()|};
                }
            }
            """,
            "RSA created without an explicit key size uses the platform default, which cannot be verified; use an explicit key size of at least 3072 bits or set KeySize before use");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_DefaultKeyOfARsaCryptoServiceProviderWithTheProviderMessage()
    {
        return VerifyMessageAsync(
            """
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    using var rsa = {|#0:new RSACryptoServiceProvider()|};
                    rsa.KeySize = 3072;
                }
            }
            """,
            "RSACryptoServiceProvider created without an explicit key size uses the platform default, which cannot be verified, and assigning KeySize afterward is not a reliable way to set it; use the constructor that takes a key size of at least 3072 bits");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFirstUseOfTheLocalSetsTheKeySizeOrInitializesIt()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] key)
                {
                    var a = RSA.Create();
                    a.KeySize = 3072;

                    var b = new RSACng { KeySize = 3072 };

                    var c = RSA.Create();
                    {|E128143:c.ImportRSAPrivateKey(key, out _)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheKeySizeIsSetOnlyAfterAnotherStatementOrThroughAnUpcast()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(bool flag)
                {
                    var a = {|E128143:RSA.Create()|};
                    if (flag)
                    {
                        a.KeySize = 3072;
                    }

                    AsymmetricAlgorithm b = {|E128143:RSA.Create()|};
                    b.KeySize = 3072;

                    var c = {|E128143:RSA.Create()|};

                    {|E128143:RSA.Create()|}.Dispose();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheUseOfTheLocalIsAnImportInsideAnotherBlock()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] key, bool flag)
                {
                    var a = {|E128143:RSA.Create()|};
                    if (flag)
                    {
                        {|E128143:a.ImportRSAPrivateKey(key, out _)|};
                    }

                    using (var b = RSA.Create())
                    {
                        b.KeySize = 3072;
                    }

                    using (var c = {|E128143:RSA.Create()|})
                    {
                        if (flag)
                        {
                            c.KeySize = 3072;
                        }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_KeyImportedThroughConstructorArguments()
    {
        return VerifyAsync("""
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M(CngKey key, IntPtr handle, CspParameters csp)
                {
                    var a = {|E128143:new RSACng(key)|};
                    var b = {|E128143:new RSACryptoServiceProvider(csp)|};
                    var c = {|E128143:new RSACryptoServiceProvider(2048)|};
                    var d = new RSACryptoServiceProvider(3072);
                    var e = {|E128143:new RSACryptoServiceProvider(2048, csp)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RsaCreatedByNameWithTheDefaultKeySizeMessage()
    {
        return VerifyMessageAsync(
            """
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var f = {|#0:CryptoConfig.CreateFromName(" System.Security.Cryptography.AsymmetricAlgorithm ")|};
                }
            }
            """,
            "RSA created by name uses the platform default key size, which cannot be verified; create the key with an explicit key size of at least 3072 bits");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RsaCreatedByNameUnlessTheNextStatementImportsAKey()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(string name, byte[] key)
                {
                    var a = (RSA)CryptoConfig.CreateFromName("RSA")!;
                    {|E128143:a.ImportRSAPrivateKey(key, out _)|};
                    var b = AsymmetricAlgorithm.Create(name);
                    var c = CryptoConfig.CreateFromName("DSA");
                    var d = {|E128143:CryptoConfig.CreateFromName("System.Security.Cryptography.RSACryptoServiceProvider")|};
                    var e = Aes.Create("AES");
                    var g = {|E128143:CryptoConfig.CreateFromName("rsa")|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ImportsAndKeyGenerationOnInstances()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(RSA rsa, ECDsa ecdsa, ECDiffieHellman dh, byte[] data, RSAParameters parameters)
                {
                    {|E128143:rsa.ImportParameters(parameters)|};
                    {|E128143:ecdsa.ImportECPrivateKey(data, out _)|};
                    {|E128143:dh.ImportFromPem("pem")|};
                    {|E128143:ecdsa.GenerateKey(ECCurve.NamedCurves.nistP256)|};
                    ecdsa.GenerateKey(ECCurve.NamedCurves.nistP384);
                    var a = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
                    var b = ecdsa.SignData(data, HashAlgorithmName.SHA256);
                    var c = rsa.ToString();
                    var d = rsa.ExportRSAPublicKey();
                    var e = ecdsa.ExportECPrivateKey();
                    var f = dh.ExportParameters(false);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_CertificateKeysAndIgnoresAnUnrelatedHelperWithTheSameName()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            using System.Security.Cryptography.X509Certificates;

            namespace System.Security.Cryptography.X509Certificates
            {
                public static class CertificateStubs
                {
                    public static int GetECDiffieHellmanPublicKey(this string text) => 0;
                }
            }

            class C
            {
                void M(X509Certificate2 cert, string text)
                {
                    var a = {|E128143:cert.GetRSAPublicKey()|};
                    var b = {|E128143:cert.GetRSAPrivateKey()|};
                    var c = {|E128143:cert.GetECDsaPublicKey()|};
                    var d = {|E128143:cert.GetECDsaPrivateKey()|};
                    var e = {|E128143:cert.GetECDiffieHellmanPublicKey()|};
                    var f = text.GetECDiffieHellmanPublicKey();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RsaCreateMethodGroupButNotOtherMethodGroups()
    {
        return VerifyAsync("""
            using System;
            using System.Security.Cryptography;
            class C
            {
                string Create() => "";

                static string Build() => "";

                void M()
                {
                    Func<int, RSA> a = {|E128143:RSA.Create|};
                    Func<RSA> b = {|E128143:RSA.Create|};
                    Func<ECDsa> c = ECDsa.Create;
                    Func<string> d = Create;
                    Func<string> e = Build;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_EllipticCurveFactoriesWithAnUnapprovedOrUnknownCurve()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(ECCurve parameter, string friendly)
                {
                    var a = {|E128143:ECDsa.Create()|};
                    var b = {|E128143:ECDiffieHellman.Create()|};
                    var c = {|E128143:ECDsa.Create(ECCurve.NamedCurves.nistP256)|};
                    var d = ECDsa.Create(ECCurve.NamedCurves.nistP384);
                    var e = ECDsa.Create(ECCurve.CreateFromFriendlyName("nistP521"));
                    var f = ECDsa.Create(ECCurve.CreateFromValue("1.3.132.0.34"));
                    var g = {|E128143:ECDsa.Create(ECCurve.CreateFromFriendlyName("nistP256"))|};
                    var h = {|E128143:ECDsa.Create(ECCurve.CreateFromFriendlyName(friendly))|};
                    var i = ECDiffieHellman.Create(ECCurve.CreateFromOid(new Oid("1.3.132.0.35")));
                    var j = {|E128143:ECDiffieHellman.Create(ECCurve.CreateFromOid(new Oid("1.2.840.10045.3.1.7")))|};
                    var k = {|E128143:ECDiffieHellman.Create(ECCurve.CreateFromOid(new Oid(friendly)))|};
                    var l = {|E128143:ECDsa.Create(parameter)|};
                    var m = {|E128143:ECDsa.Create(ECCurve.CreateFromOid(new Oid()))|};
                    var n = {|E128143:ECDsa.Create(ECCurve.CreateFromOid(null!))|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Resolves_CurvesHeldInLocalsAndReadonlyFieldsUpToTheDepthLimit()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                private static readonly ECCurve GoodField = ECCurve.NamedCurves.nistP384;
                private static readonly ECCurve BadField = ECCurve.NamedCurves.nistP256;
                private static ECCurve mutableField = ECCurve.NamedCurves.nistP384;

                void M()
                {
                    var c1 = ECCurve.NamedCurves.nistP384;
                    var c2 = c1;
                    var c3 = c2;
                    var c4 = c3;
                    var c5 = c4;
                    var a = ECDsa.Create(c4);
                    var b = {|E128143:ECDsa.Create(c5)|};
                    var c = ECDsa.Create(GoodField);
                    var d = {|E128143:ECDsa.Create(BadField)|};
                    var e = {|E128143:ECDsa.Create(mutableField)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_EllipticCurveConstructorsByCurveOrKeySize()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(int size)
                {
                    var a = new ECDsaCng(384);
                    var b = {|E128143:new ECDsaCng(256)|};
                    var c = {|E128143:new ECDsaCng(size)|};
                    var d = {|E128143:new ECDsaCng(ECCurve.NamedCurves.nistP256)|};
                    var e = new ECDsaCng(ECCurve.NamedCurves.nistP521);
                    var f = {|E128143:new ECDiffieHellmanCng()|};
                    var g = new System.Text.StringBuilder();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_KeySizeAssignmentsAndStructCurves()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;

            namespace System.Security.Cryptography
            {
                public static class StaticKeySize
                {
                    public static int KeySize { get; set; }
                }
            }

            class Holder
            {
                public int KeySize { get; set; }

                public ECCurve Curve;
            }

            class C
            {
                void M(RSA rsa, ECDsa ecdsa, Aes aes, Holder holder, int size, ECCurve unknown)
                {
                    {|E128143:rsa.KeySize = 2048|};
                    rsa.KeySize = 4096;
                    {|E128143:rsa.KeySize = size|};
                    {|E128143:ecdsa.KeySize = 256|};
                    ecdsa.KeySize = 384;
                    aes.KeySize = 128;
                    holder.KeySize = 1;
                    StaticKeySize.KeySize = 1;
                    rsa.KeySize += 1;
                    int other;
                    (rsa.KeySize, other) = ({|E128143:1024|}, 1);

                    var parameters = new ECParameters();
                    {|E128143:parameters.Curve = ECCurve.NamedCurves.nistP256|};
                    parameters.Curve = ECCurve.NamedCurves.nistP521;
                    {|E128143:parameters.Curve = unknown|};
                    holder.Curve = ECCurve.NamedCurves.nistP256;
                }
            }
            """);
    }
}
