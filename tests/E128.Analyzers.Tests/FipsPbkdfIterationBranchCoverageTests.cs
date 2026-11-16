using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsPbkdfIterationBranchCoverageTests
{
    // The ASP.NET Core key derivation package is not in the default reference set.
    // The analyzer matches it by name and namespace, so a stub with the same shape is enough.
    private const string KeyDerivationStub = """
        namespace Microsoft.AspNetCore.Cryptography.KeyDerivation
        {
            public enum KeyDerivationPrf
            {
                HMACSHA1 = 0,
                HMACSHA256 = 1,
                HMACSHA512 = 2,
            }

            public static class KeyDerivation
            {
                public static byte[] Pbkdf2(string password, byte[] salt, KeyDerivationPrf prf, int iterationCount, int numBytesRequested) => new byte[0];
            }
        }

        namespace Other
        {
            public static class Deriver
            {
                public static byte[] Pbkdf2(string password, int iterations) => new byte[0];
            }
        }
        """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsPbkdfIterationAnalyzer>(code + "\n" + KeyDerivationStub);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_DefaultIterationsAndDefaultHashOfTheShortConstructor()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] salt)
                {
                    var a = {|E128145:{|E128145:new Rfc2898DeriveBytes("password", salt)|}|};
                    var b = {|E128145:new Rfc2898DeriveBytes("password", salt, 600000)|};
                    var c = new Rfc2898DeriveBytes("password", salt, 600000, HashAlgorithmName.SHA256);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ConstructorIterationsAndHashThatAreWeakOrUnknown()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] salt, int count, HashAlgorithmName hash)
                {
                    var a = {|E128145:{|E128145:new Rfc2898DeriveBytes("password", salt, 1000, HashAlgorithmName.SHA1)|}|};
                    var b = {|E128145:{|E128145:new Rfc2898DeriveBytes("password", salt, count, hash)|}|};
                    var c = {|E128145:new Rfc2898DeriveBytes("password", salt, 600000, HashAlgorithmName.MD5)|};
                    var d = new Rfc2898DeriveBytes("password", salt, 600000, HashAlgorithmName.SHA512);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheIterationCountIsSetByInitializerOrByTheFirstStatement()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] salt, bool flag)
                {
                    var a = new Rfc2898DeriveBytes("password", salt, 600000, HashAlgorithmName.SHA256) { IterationCount = 700000 };

                    var b = {|E128145:new Rfc2898DeriveBytes("password", salt)|};
                    b.IterationCount = 700000;

                    var c = {|E128145:{|E128145:new Rfc2898DeriveBytes("password", salt)|}|};
                    if (flag)
                    {
                        c.IterationCount = 700000;
                    }

                    var d = {|E128145:{|E128145:new Rfc2898DeriveBytes("password", salt)|}|};
                    var unused = 1;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_IterationCountAssignmentsThatAreTooLowOrUnknown()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class Holder
            {
                public int IterationCount { get; set; }
            }

            class C
            {
                void M(Rfc2898DeriveBytes derive, Holder holder, int count)
                {
                    {|E128145:derive.IterationCount = 1000|};
                    derive.IterationCount = 700000;
                    {|E128145:derive.IterationCount = count|};
                    derive.IterationCount += 5;
                    holder.IterationCount = 1;
                    int other;
                    (derive.IterationCount, other) = ({|E128145:10|}, 1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_SubclassConstructorThatTakesNamedIterationsOrHash()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;

            sealed class OnlyIterations : Rfc2898DeriveBytes
            {
                public OnlyIterations(string password, int iterations)
                    : base(password, new byte[16], 600000, HashAlgorithmName.SHA256)
                {
                }
            }

            sealed class OnlyHash : Rfc2898DeriveBytes
            {
                public OnlyHash(string password, HashAlgorithmName hashAlgorithm)
                    : base(password, new byte[16], 600000, HashAlgorithmName.SHA256)
                {
                }
            }

            sealed class Neither : Rfc2898DeriveBytes
            {
                public Neither(string password)
                    : base(password, new byte[16], 600000, HashAlgorithmName.SHA256)
                {
                }
            }

            class C
            {
                void M()
                {
                    var a = {|E128145:new OnlyIterations("password", 1000)|};
                    var b = {|E128145:new OnlyHash("password", HashAlgorithmName.SHA1)|};
                    var c = new Neither("password");
                    var d = new OnlyIterations("password", 700000);
                    var e = new OnlyHash("password", HashAlgorithmName.SHA384);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_PbeParametersWithWeakIterationsOrHash()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(int count)
                {
                    var a = {|E128145:new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)|};
                    var b = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 600000);
                    var c = {|E128145:new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, count)|};
                    var d = {|E128145:new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA1, 600000)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_StaticPbkdf2WithWeakIterationsOrHash()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] salt, int count, HashAlgorithmName hash)
                {
                    var a = {|E128145:Rfc2898DeriveBytes.Pbkdf2("password", salt, 1000, HashAlgorithmName.SHA256, 32)|};
                    var b = {|E128145:Rfc2898DeriveBytes.Pbkdf2("password", salt, 600000, HashAlgorithmName.SHA1, 32)|};
                    var c = {|E128145:Rfc2898DeriveBytes.Pbkdf2("password", salt, count, HashAlgorithmName.SHA256, 32)|};
                    var d = {|E128145:Rfc2898DeriveBytes.Pbkdf2("password", salt, 600000, hash, 32)|};
                    var e = Rfc2898DeriveBytes.Pbkdf2("password", salt, 600000, HashAlgorithmName.SHA256, 32);
                    var f = Other.Deriver.Pbkdf2("password", 1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_KeyDerivationWithWeakIterationsOrPrf()
    {
        return VerifyAsync("""
            using Microsoft.AspNetCore.Cryptography.KeyDerivation;
            class C
            {
                void M(byte[] salt, int count, KeyDerivationPrf prf)
                {
                    var a = {|E128145:KeyDerivation.Pbkdf2("password", salt, KeyDerivationPrf.HMACSHA256, 1000, 32)|};
                    var b = {|E128145:KeyDerivation.Pbkdf2("password", salt, KeyDerivationPrf.HMACSHA1, 600000, 32)|};
                    var c = KeyDerivation.Pbkdf2("password", salt, KeyDerivationPrf.HMACSHA512, 600000, 32);
                    var d = {|E128145:KeyDerivation.Pbkdf2("password", salt, (KeyDerivationPrf)0, 600000, 32)|};
                    var e = KeyDerivation.Pbkdf2("password", salt, (KeyDerivationPrf)1, 600000, 32);
                    var f = {|E128145:KeyDerivation.Pbkdf2("password", salt, prf, 600000, 32)|};
                    var g = {|E128145:KeyDerivation.Pbkdf2("password", salt, KeyDerivationPrf.HMACSHA256, count, 32)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsNotAStaticPbkdf2()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(Rfc2898DeriveBytes derive)
                {
                    var a = derive.GetBytes(16);
                    var b = new System.Text.StringBuilder();
                    var c = new Rfc2898DeriveBytes("password", new byte[16], 600000, HashAlgorithmName.SHA256);
                    var d = c.Salt;
                }
            }
            """);
    }
}
