using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsUnapprovedHashBranchCoverageTests
{
    // The XML-DSig and CMS types live in packages that the default reference set omits.
    // The analyzer matches them by name and namespace, so a stub with the same shape is enough.
    private const string XmlAndCmsStubs = """
        namespace System.Security.Cryptography.Xml
        {
            public class SignedInfo { public string SignatureMethod { get; set; } = ""; }

            public class Reference { public string DigestMethod { get; set; } = ""; }

            public class EncryptionMethod
            {
                public EncryptionMethod() { }

                public EncryptionMethod(string keyAlgorithm) { KeyAlgorithm = keyAlgorithm; }

                public string KeyAlgorithm { get; set; } = "";
            }
        }

        namespace System.Security.Cryptography.Pkcs
        {
            public class CmsSigner { public object? DigestAlgorithm { get; set; } }
        }

        namespace Other
        {
            public class SignedInfo { public string SignatureMethod { get; set; } = ""; }

            public class CmsSigner { public object? DigestAlgorithm { get; set; } }
        }
        """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsUnapprovedHashAnalyzer>(code + "\n" + XmlAndCmsStubs);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForbiddenXmlUriAssignedToSignatureMethod()
    {
        return VerifyAsync("""
            using System.Security.Cryptography.Xml;
            class C
            {
                void M(SignedInfo info, Reference reference)
                {
                    {|E128071:info.SignatureMethod = "http://www.w3.org/2000/09/xmldsig#rsa-sha1"|};
                    {|E128071:reference.DigestMethod = "http://www.w3.org/2000/09/xmldsig#sha1"|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenXmlUriIsApprovedUnknownOrNotConstant()
    {
        return VerifyAsync("""
            using System.Security.Cryptography.Xml;
            class C
            {
                void M(SignedInfo info, Reference reference, string uri)
                {
                    info.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
                    reference.DigestMethod = uri;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSignatureMethodBelongsToAnotherType()
    {
        return VerifyAsync("""
            class C
            {
                string SignatureMethod = "";

                void M(Other.SignedInfo info)
                {
                    info.SignatureMethod = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";
                    SignatureMethod = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForbiddenUriInCoalesceAssignment()
    {
        return VerifyAsync("""
            using System.Security.Cryptography.Xml;
            class C
            {
                void M(EncryptionMethod method)
                {
                    {|E128071:method.KeyAlgorithm ??= "http://www.w3.org/2001/04/xmlenc#tripledes-cbc"|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForbiddenUriInTupleDeconstruction()
    {
        return VerifyAsync("""
            using System.Security.Cryptography.Xml;
            class C
            {
                void M(SignedInfo info, Reference reference)
                {
                    (info.SignatureMethod, reference.DigestMethod) = ({|E128071:"http://www.w3.org/2000/09/xmldsig#rsa-sha1"|}, "http://www.w3.org/2001/04/xmlenc#sha256");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTupleDeconstructionHasNoTupleLiteralOnTheRight()
    {
        return VerifyAsync("""
            using System.Security.Cryptography.Xml;
            class C
            {
                (string, string) Pair() => ("a", "b");

                void M(SignedInfo info, Reference reference)
                {
                    (info.SignatureMethod, reference.DigestMethod) = Pair();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_CmsSignerDigestOidForSha1()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            using System.Security.Cryptography.Pkcs;
            class C
            {
                void M(CmsSigner signer, Other.CmsSigner other, string oidText)
                {
                    {|E128071:signer.DigestAlgorithm = new Oid("1.3.14.3.2.26")|};
                    signer.DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1");
                    signer.DigestAlgorithm = new Oid(oidText);
                    signer.DigestAlgorithm = null;
                    other.DigestAlgorithm = new Oid("1.3.14.3.2.26");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_EncryptionMethodCreatedWithForbiddenUri()
    {
        return VerifyAsync("""
            using System.Security.Cryptography.Xml;
            class C
            {
                void M(string uri)
                {
                    var bad = {|E128071:new EncryptionMethod("http://www.w3.org/2001/04/xmlenc#tripledes-cbc")|};
                    var good = new EncryptionMethod("http://www.w3.org/2001/04/xmlenc#aes256-cbc");
                    var dynamic = new EncryptionMethod(uri);
                    var empty = new EncryptionMethod();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_DsaSignatureFormatterAndOaepAndPkcs1KeyExchange()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(DSA dsa, RSA rsa)
                {
                    var formatter = {|E128071:new DSASignatureFormatter(dsa)|};
                    var oaep = {|E128071:new RSAOAEPKeyExchangeFormatter(rsa)|};
                    var oaepBack = {|E128071:new RSAOAEPKeyExchangeDeformatter(rsa)|};
                    var pkcs = {|E128071:new RSAPKCS1KeyExchangeFormatter(rsa)|};
                    var pkcsBack = {|E128071:new RSAPKCS1KeyExchangeDeformatter(rsa)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_DsaSigningMembers()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                byte[] M(DSA dsa, byte[] hash, byte[] data)
                {
                    var a = {|E128071:dsa.CreateSignature(hash)|};
                    var b = {|E128071:dsa.SignData(data, HashAlgorithmName.SHA256)|};
                    return a;
                }

                bool V(DSA dsa, byte[] data, byte[] signature)
                {
                    return dsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RsaCryptoServiceProviderOaepFlag()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(RSACryptoServiceProvider rsa, byte[] data, bool flag)
                {
                    var a = {|E128071:rsa.Encrypt(data, true)|};
                    var b = {|E128071:rsa.Decrypt(data, false)|};
                    var c = {|E128071:rsa.Encrypt(data, fOAEP: flag)|};
                    var d = rsa.ExportParameters(false);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_LegacySigningMemberWithForbiddenHashName()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(RSACryptoServiceProvider rsa, RSAPKCS1SignatureFormatter formatter, RSAPKCS1SignatureDeformatter deformatter, byte[] data, byte[] hash)
                {
                    var a = {|E128071:rsa.SignData(data, "SHA1")|};
                    var b = {|E128071:rsa.SignData(data, typeof(MD5))|};
                    var c = {|E128071:rsa.SignHash(hash, "1.3.14.3.2.26")|};
                    var d = {|E128071:rsa.SignHash(hash, CryptoConfig.MapNameToOID("MD5"))|};
                    {|E128071:formatter.SetHashAlgorithm("SHA1")|};
                    deformatter.SetHashAlgorithm("SHA1");
                    var e = rsa.SignData(data, "SHA256");
                    var f = rsa.SignData(data, typeof(SHA256));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenLegacyMemberReceivesNonConstantOrApprovedHash()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(RSACryptoServiceProvider rsa, byte[] data, string name)
                {
                    var a = rsa.SignData(data, name);
                    var b = rsa.SignData(data, CryptoConfig.MapNameToOID(name));
                    var c = rsa.SignData(data, CryptoConfig.MapNameToOID("SHA256"));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenPbkdf2OrVerifyReceivesAForbiddenHashName()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(RSA rsa, byte[] data, byte[] signature, byte[] password, byte[] salt)
                {
                    var a = Rfc2898DeriveBytes.Pbkdf2(password, salt, 1000, HashAlgorithmName.SHA1, 20);
                    var b = rsa.VerifyData(data, signature, HashAlgorithmName.MD5, RSASignaturePadding.Pkcs1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ApplicationMethodThatOnlySharesAnExemptName()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class Helper
            {
                public static void Pbkdf2(HashAlgorithmName name) { }

                public void VerifyAndSign(HashAlgorithmName name) { }
            }

            class C
            {
                void M(Helper helper)
                {
                    {|E128071:Helper.Pbkdf2(HashAlgorithmName.SHA1)|};
                    {|E128071:helper.VerifyAndSign(HashAlgorithmName.MD5)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_LegacyDefaultFactoriesAndNamedFactories()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var a = {|E128071:HashAlgorithm.Create()|};
                    var b = {|E128071:HMAC.Create()|};
                    var c = {|E128071:KeyedHashAlgorithm.Create()|};
                    var d = {|E128071:HashAlgorithm.Create("MD5")|};
                    var e = {|E128071:CryptoConfig.CreateFromName("System.Security.Cryptography.SHA1")|};
                    var f = HashAlgorithm.Create("SHA256");
                    var g = CryptoConfig.CreateFromName("SHA256");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenFactoryNameIsNotConstantOrTakesNoString()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(string name)
                {
                    var a = HashAlgorithm.Create(name);
                    var b = Aes.Create();
                    var c = CryptoConfig.MapNameToOID("SHA1");
                    var d = CryptoConfig.EncodeOID("1.3.14.3.2.26");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_Rsa1PaddingOnlyWhenItReachesAnEncryptionCall()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void Use(RSAEncryptionPadding padding) { }

                void M(RSA rsa, byte[] data)
                {
                    var a = rsa.Encrypt(data, {|E128071:RSAEncryptionPadding.OaepSHA1|});
                    var b = rsa.Decrypt(data, {|E128071:RSAEncryptionPadding.Pkcs1|});
                    var c = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
                    var kept = RSAEncryptionPadding.Pkcs1;
                    Use(RSAEncryptionPadding.Pkcs1);
                    var viaVariable = {|E128071:RSAEncryptionPadding.OaepSHA1|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_TripleDesPbeAlgorithmButNotAComparison()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                bool M(PbeEncryptionAlgorithm algorithm)
                {
                    var chosen = {|E128071:PbeEncryptionAlgorithm.TripleDes3KeyPkcs12|};
                    var other = PbeEncryptionAlgorithm.Aes256Cbc;
                    return algorithm == PbeEncryptionAlgorithm.TripleDes3KeyPkcs12;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_StaticFactoryMethodGroupOfAForbiddenType()
    {
        return VerifyAsync("""
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    Func<MD5> create = {|E128071:MD5.Create|};
                    Func<SHA256> fine = SHA256.Create;
                    Func<int> unrelated = Helper.Next;
                }
            }

            static class Helper
            {
                public static int Next() => 1;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenCmsDigestValueIsNotAnOid()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            using System.Security.Cryptography.Pkcs;
            class C
            {
                void M(CmsSigner signer)
                {
                    signer.DigestAlgorithm = new System.Text.StringBuilder("1.3.14.3.2.26");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnInstanceMethodGroupIsNamedLikeAFactory()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                string Create() => "";

                void M(C other)
                {
                    Func<string> a = other.Create;
                    Func<string> b = other.ToString;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAStaticFactoryTakesANonStringFirstArgument()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var rsa = RSA.Create(2048);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OneShotHelperOfAForbiddenTypeOnce()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] data)
                {
                    var a = {|E128071:SHA1.HashData(data)|};
                    var b = SHA256.HashData(data);
                }
            }
            """);
    }
}
