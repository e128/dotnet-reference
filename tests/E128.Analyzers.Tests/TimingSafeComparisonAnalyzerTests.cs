using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TimingSafeComparisonAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task SpanEquality_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System;

                           class C
                           {
                               bool M(ReadOnlySpan<byte> computedTag, ReadOnlySpan<byte> expectedTag)
                               {
                                   return computedTag == expectedTag || computedTag != expectedTag;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ByteArrayEquality_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           class C
                           {
                               bool M(byte[] computedTag, byte[] expectedTag)
                               {
                                   return computedTag == expectedTag;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SequenceEqualOnTag_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System.Linq;

                           class C
                           {
                               bool M(byte[] computedTag, byte[] expectedTag)
                               {
                                   return {|E128157:computedTag.SequenceEqual(expectedTag)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StaticMemoryExtensionsSequenceEqual_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System;

                           class C
                           {
                               bool M(ReadOnlySpan<byte> computedTag, ReadOnlySpan<byte> expectedTag)
                               {
                                   return {|E128157:MemoryExtensions.SequenceEqual(computedTag, expectedTag)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StaticEnumerableSequenceEqual_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System.Linq;

                           class C
                           {
                               bool M(byte[] computedTag, byte[] expectedTag)
                               {
                                   return {|E128157:Enumerable.SequenceEqual(computedTag, expectedTag)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HmacResultSequenceEqual_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System;
                           using System.Security.Cryptography;

                           class C
                           {
                               bool M(byte[] key, byte[] data, byte[] expected)
                               {
                                   return {|E128157:HMACSHA256.HashData(key, data).AsSpan().SequenceEqual(expected.AsSpan())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HmacInstanceComputeHash_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System.Linq;
                           using System.Security.Cryptography;

                           class C
                           {
                               bool M(byte[] key, byte[] data, byte[] expected)
                               {
                                   using var hmac = new HMACSHA256(key);
                                   var result = hmac.ComputeHash(data);
                                   return {|E128157:result.SequenceEqual(expected)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HmacHashProperty_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System.Linq;
                           using System.Security.Cryptography;

                           class C
                           {
                               bool M(byte[] key, byte[] data, byte[] expected)
                               {
                                   using var hmac = new HMACSHA256(key);
                                   hmac.ComputeHash(data);
                                   return {|E128157:hmac.Hash.SequenceEqual(expected)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IncrementalHmacResult_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System.Linq;
                           using System.Security.Cryptography;

                           class C
                           {
                               bool M(byte[] key, byte[] data, byte[] expected)
                               {
                                   using var hash = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key);
                                   hash.AppendData(data);
                                   var result = hash.GetHashAndReset();
                                   return {|E128157:result.SequenceEqual(expected)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IncrementalPlainHashResult_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System.Linq;
                           using System.Security.Cryptography;

                           class C
                           {
                               bool M(byte[] data, byte[] expected)
                               {
                                   using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                                   hash.AppendData(data);
                                   var result = hash.GetHashAndReset();
                                   return result.SequenceEqual(expected);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LowercaseTagName_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System.Linq;

                           class C
                           {
                               bool M(byte[] computedtag, byte[] expected)
                               {
                                   return {|E128157:computedtag.SequenceEqual(expected)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ContentHashEquality_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System;

                           class C
                           {
                               bool M(ReadOnlySpan<byte> firstHash, ReadOnlySpan<byte> secondHash)
                               {
                                   return firstHash == secondHash;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixedTimeEquals_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System.Security.Cryptography;

                           class C
                           {
                               bool M(byte[] computedTag, byte[] expectedTag)
                               {
                                   return CryptographicOperations.FixedTimeEquals(computedTag, expectedTag);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HexOfHmacStringEquality_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System;
                           using System.Security.Cryptography;

                           class C
                           {
                               bool M(byte[] key, byte[] data, string supplied)
                               {
                                   return {|E128157:Convert.ToHexString(HMACSHA256.HashData(key, data)) == supplied|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Base64OfHmacLocalStringNotEquals_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System;
                           using System.Security.Cryptography;

                           class C
                           {
                               bool M(byte[] key, byte[] data, string supplied)
                               {
                                   var encoded = Convert.ToBase64String(HMACSHA256.HashData(key, data));
                                   return {|E128157:encoded != supplied|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringEqualsOnSignatureName_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           using System;

                           class C
                           {
                               bool M(string signature, string expectedSignature)
                               {
                                   return {|E128157:string.Equals(signature, expectedSignature, StringComparison.Ordinal)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InstanceEqualsOnMacName_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           class C
                           {
                               bool M(string requestMac, string other)
                               {
                                   return {|E128157:requestMac.Equals(other)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TokenAgainstExpectedToken_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           class C
                           {
                               bool M(string token, string expectedToken)
                               {
                                   return {|E128157:token == expectedToken|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringTagAgainstLiteral_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           class C
                           {
                               bool M(string htmlTag)
                               {
                                   return htmlTag == "div";
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringHashNames_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>("""
                           class C
                           {
                               bool M(string firstHash, string secondHash, string name, string title)
                               {
                                   return firstHash == secondHash || name == title;
                               }
                           }
                           """);
    }
}
