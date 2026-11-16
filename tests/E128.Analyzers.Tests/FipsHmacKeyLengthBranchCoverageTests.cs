using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsHmacKeyLengthBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsHmacKeyLengthAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ShortKeyFromEachLiteralShape()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] data)
                {
                    var a = {|E128151:new HMACSHA256(new byte[16])|};
                    var b = {|E128151:new HMACSHA256(new byte[] { 1, 2, 3 })|};
                    var c = {|E128151:new HMACSHA256([1, 2, 3])|};
                    var d = {|E128151:HMACSHA256.HashData(stackalloc byte[16], data)|};
                    var e = {|E128151:HMACSHA256.HashData(stackalloc byte[] { 1, 2 }, data)|};
                    var f = {|E128151:HMACSHA256.HashData(stackalloc[] { (byte)1, (byte)2, (byte)3 }, data)|};
                    var g = {|E128151:HMACSHA256.HashData(source: data, key: new byte[8])|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeyLengthIsUnknownOrLongEnough()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] data, byte[] key, int size)
                {
                    var a = new HMACSHA256(key);
                    var b = new HMACSHA256(new byte[size]);
                    var c = new HMACSHA256([.. key]);
                    var d = new HMACSHA256(new byte[32]);
                    var e = new HMACSHA384(new byte[48]);
                    var f = new HMACSHA512(new byte[64]);
                    var g = new HMACSHA256();
                    var h = HMACSHA256.HashData(stackalloc byte[size], data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_KeyThatDoesNotMeetTheLargerHashOutput()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var a = {|E128151:new HMACSHA384(new byte[40])|};
                    var b = {|E128151:new HMACSHA512(new byte[60])|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsNotACoveredHmac()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class HMACSHA256Wrapper
            {
                public HMACSHA256Wrapper(byte[] key) { }
            }

            class C
            {
                void M()
                {
                    var a = new HMACMD5(new byte[4]);
                    var b = new HMACSHA256Wrapper(new byte[4]);
                    var c = new System.Text.StringBuilder();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ShortKeyFromEncodedStrings()
    {
        return VerifyAsync("""
            using System;
            using System.Security.Cryptography;
            using System.Text;
            class C
            {
                void M(byte[] data)
                {
                    var a = {|E128151:HMACSHA256.HashData(Encoding.UTF8.GetBytes("short"), data)|};
                    var b = {|E128151:HMACSHA256.HashData(Encoding.ASCII.GetBytes("short"), data)|};
                    var c = {|E128151:HMACSHA256.HashData(Encoding.Unicode.GetBytes("short"), data)|};
                    var d = {|E128151:HMACSHA256.HashData(Encoding.Latin1.GetBytes("short"), data)|};
                    var e = {|E128151:HMACSHA256.HashData(Encoding.UTF32.GetBytes("short"), data)|};
                    var f = {|E128151:HMACSHA256.HashData(Convert.FromBase64String("AAAA"), data)|};
                    var g = {|E128151:HMACSHA256.HashData(Convert.FromHexString("0102"), data)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheEncodedStringIsUnreadable()
    {
        return VerifyAsync("""
            using System;
            using System.Security.Cryptography;
            using System.Text;
            class C
            {
                void M(byte[] data, string text, Encoding encoding)
                {
                    var a = HMACSHA256.HashData(Encoding.UTF8.GetBytes(text), data);
                    var b = HMACSHA256.HashData(encoding.GetBytes("short"), data);
                    var c = HMACSHA256.HashData(Encoding.Default.GetBytes("short"), data);
                    var d = HMACSHA256.HashData(Convert.FromBase64String("not base64!"), data);
                    var e = HMACSHA256.HashData(Convert.FromHexString("abc"), data);
                    var f = HMACSHA256.HashData(Convert.FromHexString("zz"), data);
                    var g = HMACSHA256.HashData(Convert.FromBase64String(text), data);
                    var h = HMACSHA256.HashData(Convert.FromHexString(text), data);
                    var i = HMACSHA256.HashData(Convert.FromBase64CharArray(new char[4], 0, 4), data);
                    var j = HMACSHA256.HashData(Encoding.UTF8.GetBytes(new char[4]), data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ShortKeyFromSlicesAndRanges()
    {
        return VerifyAsync("""
            using System;
            using System.Runtime.CompilerServices;
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] data)
                {
                    var a = {|E128151:HMACSHA256.HashData(new byte[64].AsSpan(0, 8), data)|};
                    var b = {|E128151:HMACSHA256.HashData(new byte[34].AsSpan(8), data)|};
                    var c = {|E128151:HMACSHA256.HashData(new byte[64].AsSpan(..8), data)|};
                    var d = {|E128151:HMACSHA256.HashData(new byte[64].AsSpan(8..16), data)|};
                    var e = {|E128151:HMACSHA256.HashData(new byte[40].AsSpan(^4..), data)|};
                    var f = {|E128151:HMACSHA256.HashData(new byte[40].AsSpan(^36..^8), data)|};
                    var h = {|E128151:HMACSHA256.HashData(new byte[64].AsSpan().Slice(2, 8), data)|};
                    var i = {|E128151:HMACSHA256.HashData(new byte[34].AsSpan().Slice(4), data)|};
                    var j = {|E128151:HMACSHA256.HashData(RuntimeHelpers.GetSubArray(new byte[64], 0..8), data)|};
                    var k = {|E128151:HMACSHA256.HashData((new byte[64])[..8], data)|};
                    var l = {|E128151:HMACSHA256.HashData(new byte[8].AsSpan(), data)|};
                    var m = {|E128151:HMACSHA256.HashData(new byte[64].AsSpan(length: 8, start: 0), data)|};
                    var n = {|E128151:HMACSHA256.HashData(new byte[64].AsSpan()[..8], data)|};
                    var o = {|E128151:HMACSHA256.HashData(new byte[40].AsSpan()[^4..], data)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenASliceBoundIsUnknownOrOutOfOrder()
    {
        return VerifyAsync("""
            using System;
            using System.Runtime.CompilerServices;
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] data, byte[] key, int start, int count, Range range)
                {
                    var a = HMACSHA256.HashData(key.AsSpan(4), data);
                    var b = HMACSHA256.HashData(new byte[40].AsSpan(start), data);
                    var c = HMACSHA256.HashData(new byte[40].AsSpan(64), data);
                    var d = HMACSHA256.HashData(new byte[40].AsSpan(0, count), data);
                    var e = HMACSHA256.HashData(new byte[40].AsSpan(0, -1), data);
                    var f = HMACSHA256.HashData(new byte[40].AsSpan(range), data);
                    var g = HMACSHA256.HashData(new byte[40].AsSpan(16..8), data);
                    var h = HMACSHA256.HashData(new byte[40].AsSpan(^64..), data);
                    var i = HMACSHA256.HashData(key.AsSpan(^4..), data);
                    var j = HMACSHA256.HashData(key.AsSpan(..^4), data);
                    var k = HMACSHA256.HashData(key.AsSpan(..), data);
                    var l = HMACSHA256.HashData(new byte[40].AsSpan(start..), data);
                    var m = HMACSHA256.HashData(new byte[40].AsSpan(..start), data);
                    var n = HMACSHA256.HashData(RuntimeHelpers.GetSubArray(key, range), data);
                    var o = HMACSHA256.HashData(key.AsSpan().Slice(start), data);
                    var p = HMACSHA256.HashData(key[..start], data);
                    var q = HMACSHA256.HashData(key.AsSpan()[..start], data);
                    var r = HMACSHA256.HashData((new byte[40])[^64..], data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ShortKeyHeldInAFieldOrLocal()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                private static readonly byte[] ShortField = new byte[8];
                private static readonly byte[] CtorField;
                private byte[] mutable = new byte[8];

                static C()
                {
                    CtorField = new byte[8];
                }

                void M(byte[] data)
                {
                    var local = new byte[8];
                    var alias = local;
                    var reassigned = new byte[8];
                    reassigned = new byte[64];
                    var a = {|E128151:HMACSHA256.HashData(local, data)|};
                    var b = {|E128151:HMACSHA256.HashData(alias, data)|};
                    var c = {|E128151:HMACSHA256.HashData(ShortField, data)|};
                    var d = HMACSHA256.HashData(reassigned, data);
                    var e = HMACSHA256.HashData(CtorField, data);
                    var f = HMACSHA256.HashData(mutable, data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_HashNameBasedHmacHelpers()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(HashAlgorithmName dynamicName)
                {
                    var name = HashAlgorithmName.SHA256;
                    var a = {|E128151:IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, new byte[8])|};
                    var b = {|E128151:IncrementalHash.CreateHMAC(name, new byte[8])|};
                    var c = {|E128151:IncrementalHash.CreateHMAC(HashAlgorithmName.SHA3_256, new byte[8])|};
                    var d = {|E128151:IncrementalHash.CreateHMAC(HashAlgorithmName.FromOid("2.16.840.1.101.3.4.2.8"), new byte[8])|};
                    var e = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, new byte[40]);
                    var f = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA1, new byte[8]);
                    var g = IncrementalHash.CreateHMAC(dynamicName, new byte[8]);
                    var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_HmacNamedHelpersInTheCryptographyNamespace()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;

            namespace System.Security.Cryptography
            {
                public static class HmacExtras
                {
                    public static void NoHashNameHmac(byte[] key) { }

                    public static void TwoHashNamesHmac(byte[] key, HashAlgorithmName first, HashAlgorithmName second) { }
                }
            }

            class C
            {
                void M(byte[][] jagged, HashAlgorithmName unknown)
                {
                    HmacExtras.NoHashNameHmac(new byte[8]);
                    {|E128151:HmacExtras.TwoHashNamesHmac(new byte[8], unknown, HashAlgorithmName.SHA256)|};
                    var a = HMACSHA256.HashData(jagged[0], new byte[1]);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnHmacNamedMethodComesFromOutsideCryptography()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            static class Own
            {
                public static void MakeHmac(HashAlgorithmName name, byte[] key) { }
            }

            class C
            {
                void M()
                {
                    Own.MakeHmac(HashAlgorithmName.SHA256, new byte[8]);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ShortKeyAssignedToTheKeyProperty()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(HMACSHA256 typed, HMAC unknown)
                {
                    {|E128151:typed.Key = new byte[8]|};
                    typed.Key = new byte[32];
                    unknown.Key = new byte[8];

                    HMAC based = new HMACSHA256(new byte[32]);
                    {|E128151:based.Key = new byte[8]|};

                    HMAC plain = HMAC.Create("HMACSHA256");
                    plain.Key = new byte[8];

                    (typed.Key, _) = ({|E128151:new byte[8]|}, 1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAKeyNamedMemberIsNotAnHmacKey()
    {
        return VerifyAsync("""
            class Holder
            {
                public byte[] Key { get; set; } = new byte[0];

                public byte[] KeyField = new byte[0];

                public int Count { get; set; }
            }

            class C
            {
                void M(Holder holder)
                {
                    holder.Key = new byte[8];
                    holder.KeyField = new byte[8];
                    holder.Count = 1;
                    holder.Count += 1;
                }
            }
            """);
    }
}
