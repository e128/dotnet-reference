using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TimingSafeComparisonBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<TimingSafeComparisonAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_SequenceEqualOnSpansWhenEitherSideLooksSecret()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                bool M(Span<byte> computedTag, ReadOnlySpan<byte> expectedTag, byte[] data, int[] numbers, int[] otherNumbers)
                {
                    var a = {|E128157:computedTag.SequenceEqual(expectedTag)|};
                    var b = {|E128157:data.AsSpan().SequenceEqual(expectedTag)|};
                    var c = {|E128157:expectedTag.SequenceEqual(data)|};
                    var d = data.AsSpan().SequenceEqual(data);
                    var e = numbers.AsSpan().SequenceEqual(otherNumbers);
                    return a && b && c && d && e;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenSequenceEqualBelongsToAnotherTypeOrTakesAComparer()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Linq;
            static class Own
            {
                public static bool SequenceEqual(byte[] a, byte[] b) => true;
            }

            class C
            {
                bool M(byte[] computedTag, byte[] expectedTag, IEqualityComparer<byte> comparer)
                {
                    var a = Own.SequenceEqual(computedTag, expectedTag);
                    var b = computedTag.SequenceEqual(expectedTag, comparer);
                    return a && b;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_SecretNamesBySuffixOrSegment()
    {
        return VerifyAsync("""
            using System.Linq;
            class C
            {
                bool M(byte[] my_tag_value, byte[] received_hmac, byte[] other, byte[] signature, byte[] plain)
                {
                    var a = {|E128157:my_tag_value.SequenceEqual(other)|};
                    var b = {|E128157:other.SequenceEqual(received_hmac)|};
                    var c = {|E128157:signature.SequenceEqual(plain)|};
                    var d = plain.SequenceEqual(other);
                    return a && b && c && d;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ValuesProducedByHmacMembersAndEncoders()
    {
        return VerifyAsync("""
            using System;
            using System.Buffers.Text;
            using System.Linq;
            using System.Security.Cryptography;
            class C
            {
                string Field = "";

                bool M(HMACSHA256 hmac, byte[] key, byte[] data, byte[] other, string text)
                {
                    var a = {|E128157:hmac.ComputeHash(data).SequenceEqual(other)|};
                    var b = {|E128157:HMACSHA256.HashData(key, data).SequenceEqual(other)|};
                    var c = {|E128157:(hmac.Hash).SequenceEqual(other)|};
                    var d = {|E128157:Convert.ToHexString(hmac.ComputeHash(data)) == text|};
                    var e = {|E128157:Convert.ToBase64String(hmac.ComputeHash(data)).ToLowerInvariant() == text|};
                    var f = {|E128157:Base64Url.EncodeToString(hmac.ComputeHash(data)) == text|};
                    var computed = hmac.ComputeHash(data);
                    var g = {|E128157:computed.SequenceEqual(other)|};
                    var encoded = Convert.ToHexString(hmac.ComputeHash(data));
                    var h = {|E128157:encoded == text|};
                    var i = {|E128157:text.Equals(encoded)|};
                    return a && b && c && d && e && f && g && h && i;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheProducerIsNotAnHmacOrTheEncoderIsUnknown()
    {
        return VerifyAsync("""
            using System;
            using System.Linq;
            using System.Security.Cryptography;
            class Holder
            {
                public byte[] Hash = new byte[0];
            }

            class C
            {
                bool M(SHA256 sha, Holder holder, byte[] data, byte[] other, string text)
                {
                    var a = sha.ComputeHash(data).SequenceEqual(other);
                    var b = holder.Hash.SequenceEqual(other);
                    var c = sha.Hash!.SequenceEqual(other);
                    var d = Convert.ToHexString(sha.ComputeHash(data)) == text;
                    var e = Convert.ToHexStringLower(other) == text;
                    var f = string.Join(",", other) == text;
                    var g = Convert.FromHexString(text).SequenceEqual(other);
                    var h = (a ? other : data).SequenceEqual(other);
                    return a && b && c && d && e && f && g && h;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_IncrementalHmacReadersThroughALocalOrAFieldOrDirectly()
    {
        return VerifyAsync("""
            using System.Linq;
            using System.Security.Cryptography;
            class C
            {
                private readonly IncrementalHash field = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, new byte[32]);

                bool M(byte[] key, byte[] other, IncrementalHash parameter)
                {
                    var local = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key);
                    var a = {|E128157:local.GetCurrentHash().SequenceEqual(other)|};
                    var b = {|E128157:field.GetHashAndReset().SequenceEqual(other)|};
                    var c = {|E128157:IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key).GetHashAndReset().SequenceEqual(other)|};
                    var d = parameter.GetCurrentHash().SequenceEqual(other);
                    var plain = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var e = plain.GetCurrentHash().SequenceEqual(other);
                    return a && b && c && d && e;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_StringEqualityFormsForSecretStrings()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                bool M(string computedTag, string expectedTag, string name, string other, object boxed)
                {
                    var a = {|E128157:computedTag == expectedTag|};
                    var b = {|E128157:computedTag != name|};
                    var c = {|E128157:string.Equals(computedTag, name)|};
                    var d = {|E128157:name.Equals(computedTag)|};
                    var e = {|E128157:string.Equals(name, computedTag, StringComparison.Ordinal)|};
                    var f = {|E128157:object.Equals(computedTag, name)|};
                    var g = {|E128157:Equals(computedTag, name)|};
                    var h = name.Equals(other);
                    var i = computedTag == "constant";
                    var j = computedTag.Equals(boxed);
                    var k = Equals(name);
                    var l = name.GetHashCode() == other.GetHashCode();
                    return a && b && c && d && e && f && g && h && i && j && k && l;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_StringsOnlyWhenBothNamesCarryAWeakWord()
    {
        return VerifyAsync("""
            class C
            {
                bool M(string token, string expectedToken, string digest, string hash, string contentHash, string other, string name)
                {
                    var a = {|E128157:token == expectedToken|};
                    var b = {|E128157:digest == hash|};
                    var c = {|E128157:hash == digest|};
                    var d = hash == contentHash;
                    var e = token == other;
                    var f = other == token;
                    var g = token == name + other;
                    var h = token == null;
                    var i = token.Length == other.Length;
                    return a && b && c && d && e && f && g && h && i;
                }
            }
            """);
    }
}
