using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InsecureRandomInCryptoContextBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<InsecureRandomInCryptoContextAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NextBytesIntoASensitiveBufferThroughSliceCalls()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                byte[] GetBuffer() => new byte[8];

                void M(Random random, byte[] keyBuffer, byte[] plain)
                {
                    {|E128075:random.NextBytes(keyBuffer.AsSpan(0, 8))|};
                    {|E128075:random.NextBytes(keyBuffer.AsSpan())|};
                    {|E128075:random.NextBytes(keyBuffer)|};
                    Span<byte> keySpan = keyBuffer;
                    {|E128075:random.NextBytes(keySpan.Slice(0, 4))|};
                    random.NextBytes(plain.AsSpan());
                    random.NextBytes(GetBuffer());
                    random.NextBytes(stackalloc byte[8]);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_SensitiveDestinationsReachedThroughValueExpressions()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                static object Box(int value) => value;

                static T Echo<T>(T value) => value;

                void M(Random random, int count)
                {
                    var tokenBytes = BitConverter.GetBytes({|E128075:random.Next()|});
                    var otp = Math.Abs({|E128075:random.Next()|});
                    var pinText = {|E128075:random.Next()|}.ToString();
                    var nonce = $"{ {|E128075:random.Next()|} }";
                    var salt = (long){|E128075:random.Next()|} + 1;
                    var iv = -{|E128075:random.Next()|};
                    var secret = count > 0 ? {|E128075:random.Next()|} : 0;
                    int? key = null;
                    key ??= {|E128075:random.Next()|};
                    var tokenEcho = Echo({|E128075:random.Next()|});
                    var tokenStamp = DateTime.FromBinary({|E128075:random.Next()|});
                    var plain = Math.Abs(random.Next());
                    var tokenObject = Box(random.Next());
                    var tokenSpan = TimeSpan.FromTicks(random.Next());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_AssignmentsToSensitiveParametersAndIgnoresOtherTargets()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                int[] values = new int[4];

                void Fill(Random random, ref int token, out int pin, int[] numbers)
                {
                    token = {|E128075:random.Next()|};
                    pin = {|E128075:random.Next()|};
                    numbers[0] = random.Next();
                    values[random.Next(4)] = 1;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_CreationsInAFileThatUsesAspNetCoreCryptography()
    {
        return VerifyAsync("""
            using System;

            namespace Microsoft.AspNetCore.Cryptography.KeyDerivation
            {
                public static class KeyDerivation
                {
                    public static int Derive() => 1;
                }
            }

            class C
            {
                int M()
                {
                    var d = Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivation.Derive();
                    return d + {|E128075:new Random()|}.Next();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_CreationsInAFileThatUsesAnAttributeWrittenWithoutItsSuffix()
    {
        return VerifyAsync("""
            using System;

            namespace System.Security.Cryptography
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MarkerAttribute : Attribute
                {
                }

                public sealed class AttributeSuffixOnly : Attribute
                {
                }
            }

            [System.Security.Cryptography.Marker]
            class C
            {
                int M()
                {
                    return {|E128075:new Random()|}.Next();
                }
            }

            class D
            {
                int M()
                {
                    return {|E128075:new Random()|}.Next();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFileNeverNamesACryptographyTypeAndNothingIsSensitive()
    {
        return VerifyAsync("""
            using System;

            namespace Own.Cryptography
            {
                public static class Helper
                {
                    public static int Value() => 1;
                }
            }

            class C
            {
                int M()
                {
                    var d = Own.Cryptography.Helper.Value();
                    return d + new Random().Next();
                }
            }
            """);
    }
}
