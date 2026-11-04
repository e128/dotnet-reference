using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TimingSafeComparisonAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TimingSafeComparisonAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TagEquality_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               bool M(ReadOnlySpan<byte> computedTag, ReadOnlySpan<byte> expectedTag)
                               {
                                   return {|E128157:computedTag == expectedTag|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TagInequality_Fires()
    {
        return VerifyAsync("""
                           using System;

                           class C
                           {
                               bool M(ReadOnlySpan<byte> computedTag, ReadOnlySpan<byte> expectedTag)
                               {
                                   return {|E128157:computedTag != expectedTag|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ByteArrayEquality_DoesNotFire()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
    public Task HmacResultSpanEquality_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;

                           class C
                           {
                               bool M(byte[] key, byte[] data, byte[] expected)
                               {
                                   return {|E128157:HMACSHA256.HashData(key, data).AsSpan() == expected.AsSpan()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LowercaseTagName_Fires()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
}
