using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsSymbolHelpersBranchCoverageGapTests
{
    private static Task VerifyHmacAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsHmacKeyLengthAnalyzer>(code);
    }

    private static Task VerifyPbkdfAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsPbkdfIterationAnalyzer>(code);
    }

    private static Task VerifyHashAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsUnapprovedHashAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeyIsSlicedByARangeVariable()
    {
        return VerifyHmacAsync("""
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] data, byte[] key, Range range)
                {
                    var a = HMACSHA256.HashData(key.AsSpan()[range], data);
                    var b = HMACSHA256.HashData(key[range], data);
                    var c = HMACSHA256.HashData(new byte[8].AsSpan()[range], data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ShortKeyHeldInAChainOfLocalsWithinTheDepthLimit()
    {
        return VerifyHmacAsync("""
            using System.Security.Cryptography;
            using System.Text;
            class C
            {
                void M(byte[] data)
                {
                    var encoded1 = Encoding.UTF8.GetBytes("short");
                    var encoded2 = encoded1;
                    var encoded3 = encoded2;
                    var a = {|E128151:HMACSHA256.HashData(encoded3, data)|};

                    var ranged1 = (new byte[64])[..8];
                    var ranged2 = ranged1;
                    var ranged3 = ranged2;
                    var b = {|E128151:HMACSHA256.HashData(ranged3, data)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeyLiesBeyondTheLocalChainDepthLimit()
    {
        return VerifyHmacAsync("""
            using System.Security.Cryptography;
            using System.Text;
            class C
            {
                void M(byte[] data)
                {
                    var encoded1 = Encoding.UTF8.GetBytes("short");
                    var encoded2 = encoded1;
                    var encoded3 = encoded2;
                    var encoded4 = encoded3;
                    var a = HMACSHA256.HashData(encoded4, data);

                    var ranged1 = (new byte[64])[..8];
                    var ranged2 = ranged1;
                    var ranged3 = ranged2;
                    var ranged4 = ranged3;
                    var b = HMACSHA256.HashData(ranged4, data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnEncodedConstantExceedsTheLengthCap()
    {
        return VerifyHmacAsync("""
            using System;
            using System.Security.Cryptography;
            using System.Text;
            class C
            {
                private const string K0 = "0123456789abcdef";
                private const string K1 = K0 + K0;
                private const string K2 = K1 + K1;
                private const string K3 = K2 + K2;
                private const string K4 = K3 + K3;
                private const string K5 = K4 + K4;
                private const string K6 = K5 + K5;
                private const string K7 = K6 + K6;
                private const string K8 = K7 + K7;
                private const string K9 = K8 + K8;
                private const string K10 = K9 + K9;
                private const string K11 = K10 + K10;
                private const string K12 = K11 + K11;
                private const string K13 = K12 + K12;
                private const string K14 = K13 + K13;
                private const string K15 = K14 + K14;
                private const string K16 = K15 + K15;
                private const string K17 = K16 + K16;

                void M(byte[] data)
                {
                    var a = HMACSHA256.HashData(Encoding.UTF8.GetBytes(K17), data);
                    var b = HMACSHA256.HashData(Convert.FromBase64String(K17), data);
                    var c = HMACSHA256.HashData(Convert.FromHexString(K17), data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeyComesFromAStaticCallThatIsNotAnEncoding()
    {
        return VerifyHmacAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] data)
                {
                    var a = HMACSHA256.HashData(RandomNumberGenerator.GetBytes(8), data);
                    var b = HMACSHA256.HashData(System.GC.AllocateArray<byte>(8), data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ShortKeyFromAnIndexStartedSlice()
    {
        return VerifyHmacAsync("""
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] data)
                {
                    var a = {|E128151:HMACSHA256.HashData(new byte[40].AsSpan(^4), data)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForSliceHelpersThatTakeNoSourceArgument()
    {
        return VerifyHmacAsync("""
            using System.Security.Cryptography;

            namespace System
            {
                public static class Odd
                {
                    public static byte[] Slice() => new byte[8];
                }
            }

            namespace System.Runtime.CompilerServices
            {
                public static class RuntimeHelpers
                {
                    public static T[] GetSubArray<T>(T[] array) => array;
                }
            }

            class C
            {
                void M(byte[] data)
                {
                    var a = HMACSHA256.HashData(System.Odd.Slice(), data);
                    var b = HMACSHA256.HashData(System.Runtime.CompilerServices.RuntimeHelpers.GetSubArray(new byte[8]), data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeyIsNotAByteBuffer()
    {
        return VerifyHmacAsync("""
            namespace System.Security.Cryptography
            {
                public static class HMACSHA256
                {
                    public static byte[] HashData(object key, byte[] source) => source;
                }
            }

            class C
            {
                void M(byte[] data)
                {
                    var a = System.Security.Cryptography.HMACSHA256.HashData(new byte[2, 3], data);
                    var b = System.Security.Cryptography.HMACSHA256.HashData(System.Convert.ToInt32("5"), data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeyOwnerIsATypeParameter()
    {
        return VerifyHmacAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M<T>(T hmac) where T : HMAC
                {
                    hmac.Key = new byte[8];
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NestedConditionalsWithinTheDepthLimit()
    {
        return VerifyHashAsync("""
            using System.Security.Cryptography;
            class C
            {
                static void Use(HashAlgorithmName name) { }

                void M(bool f)
                {
                    {|E128071:Use(f ? (f ? (f ? (f ? HashAlgorithmName.MD5 : HashAlgorithmName.SHA256) : HashAlgorithmName.SHA256) : HashAlgorithmName.SHA256) : HashAlgorithmName.SHA256)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForNestedConditionalsBeyondTheDepthLimit()
    {
        return VerifyHashAsync("""
            using System.Security.Cryptography;
            class C
            {
                static void Use(HashAlgorithmName name) { }

                void M(bool f)
                {
                    Use(f ? (f ? (f ? (f ? (f ? HashAlgorithmName.MD5 : HashAlgorithmName.SHA256) : HashAlgorithmName.SHA256) : HashAlgorithmName.SHA256) : HashAlgorithmName.SHA256) : HashAlgorithmName.SHA256);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForFromOidLookalikesAndUnknownOids()
    {
        return VerifyHashAsync("""
            using System.Security.Cryptography;
            static class Other
            {
                public static HashAlgorithmName FromOid(string oid) => HashAlgorithmName.SHA256;
            }

            class Maker
            {
                public HashAlgorithmName FromOid(string oid) => HashAlgorithmName.SHA256;
            }

            class C
            {
                static void Use(HashAlgorithmName name) { }

                void M(Maker maker)
                {
                    Use(Other.FromOid("1.2.840.113549.2.5"));
                    Use(maker.FromOid("1.2.840.113549.2.5"));
                    Use(HashAlgorithmName.FromOid("9.9.9"));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAStackallocWithoutSizeOrInitializer()
    {
        return VerifyHmacAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] data)
                {
                    var a = HMACSHA256.HashData(stackalloc byte{|CS1586:[]|}, data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnInitializerSetsAnotherPropertyThanIterationCount()
    {
        return VerifyPbkdfAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] salt)
                {
                    var a = {|E128145:{|E128145:new Rfc2898DeriveBytes("password", salt) { Salt = salt }|}|};
                    var b = {|E128145:{|E128145:new Rfc2898DeriveBytes("password", salt) { Salt = { [0] = 1 } }|}|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALocalIsFirstUsedThroughReorderedNamedArguments()
    {
        return VerifyPbkdfAsync("""
            using System.Security.Cryptography;
            class C
            {
                static void Use(Rfc2898DeriveBytes first, Rfc2898DeriveBytes second) { }

                void M(byte[] salt)
                {
                    var derive = {|E128145:{|E128145:new Rfc2898DeriveBytes("password", salt)|}|};
                    Use(second: derive, first: derive);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnlyMutableHashNameInitializers()
    {
        return VerifyHashAsync("""
            using System.Security.Cryptography;
            class C
            {
                static int number = 1;
                static readonly HashAlgorithmName ReadOnlyName = HashAlgorithmName.SHA256;
                static HashAlgorithmName SafeName = HashAlgorithmName.SHA256;
                static HashAlgorithmName UnsafeName = {|E128071:HashAlgorithmName.MD5|};

                int Count { get; set; } = 3;

                HashAlgorithmName Name { get; set; } = HashAlgorithmName.SHA256;

                HashAlgorithmName Other { get; set; } = {|E128071:HashAlgorithmName.MD5|};
            }
            """);
    }
}
