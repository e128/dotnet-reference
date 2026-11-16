using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsDirectInstantiationBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsDirectInstantiationAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ManagedAndCngTypesThatAFactoryCanReplace()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(int size, ECCurve curve, string name)
                {
                    var a = {|E128142:new AesManaged()|};
                    var b = {|E128142:new AesCng()|};
                    var c = {|E128142:new SHA256Managed()|};
                    var e = {|E128142:new RSACng(3072)|};
                    var f = {|E128142:new ECDsaCng(curve)|};
                    var g = {|E128142:new ECDiffieHellmanCng(curve)|};
                    var h = {|E128142:new DSACng(2048)|};
                    var i = {|E128142:new AesCng(name)|};
                    var j = {|E128142:new RSACng(size)|};
                    var k = {|E128142:new RSACng()|};
                    var l = {|E128142:new ECDsaCng(size)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenACngConstructorOpensAPersistedKey()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(CngKey key)
                {
                    var a = new RSACng(key);
                    var b = new ECDsaCng(key);
                    var c = new AesCng("key", CngProvider.MicrosoftSoftwareKeyStorageProvider);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RijndaelFactoryAndTypes()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var a = {|E128142:Rijndael.Create()|};
                    var b = {|E128142:new RijndaelManaged()|};
                    var c = Aes.Create();
                    var d = new Other();
                }
            }

            class Other
            {
                public static Other Create() => new Other();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_AManagedNamedTypeWithNoBaseNameUsingTheGenericFactoryAdvice()
    {
        return VerifyAsync("""
            namespace System.Security.Cryptography
            {
                public class Managed
                {
                }

                public class Cng
                {
                    public Cng(int value) { }
                }
            }

            class C
            {
                void M()
                {
                    var a = {|E128142:new System.Security.Cryptography.Managed()|};
                    var b = {|E128142:new System.Security.Cryptography.Cng(1)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ARijndaelSymmetricAlgorithmOutsideTheCryptographyNamespace()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;

            namespace Vendor
            {
                public sealed class Rijndael : SymmetricAlgorithm
                {
                    public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[]? rgbIV) => throw new System.NotSupportedException();

                    public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[]? rgbIV) => throw new System.NotSupportedException();

                    public override void GenerateIV() { }

                    public override void GenerateKey() { }
                }

                public sealed class RijndaelManaged
                {
                }

                public sealed class Plain
                {
                }
            }

            class C
            {
                void M()
                {
                    var a = {|E128142:new Vendor.Rijndael()|};
                    var b = new Vendor.RijndaelManaged();
                    var c = new Vendor.Plain();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnotherRuleOwnsTheTypeOrTheTypeIsNotReplaceable()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class BlowfishManaged
            {
            }

            class C
            {
                void M(byte[] key)
                {
                    var a = new SHA1Managed();
                    var c = new BlowfishManaged();
                    var d = new HMACSHA256(key);
                    var e = new System.Text.StringBuilder();
                }
            }
            """);
    }
}
