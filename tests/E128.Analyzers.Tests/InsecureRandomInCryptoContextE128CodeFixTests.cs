using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InsecureRandomInCryptoContextE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider>(source, fixedCode);
    }

    private static Task VerifyNoFixAsync(string source, params DiagnosticResult[] expected)
    {
        return CodeFixTestRunner.NoFixAsync<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider>(source, expected: expected);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RandomSharedNext_OneArg_FixedToGetInt32()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var bytes = RandomNumberGenerator.GetBytes(1);
                    var x = {|E128075:Random.Shared|}.Next(10);
                }
            }
            """,
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var bytes = RandomNumberGenerator.GetBytes(1);
                    var x = RandomNumberGenerator.GetInt32(10);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RandomSharedNext_TwoArgs_FixedToGetInt32()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var bytes = RandomNumberGenerator.GetBytes(1);
                    var x = {|E128075:Random.Shared|}.Next(1, 100);
                }
            }
            """,
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var bytes = RandomNumberGenerator.GetBytes(1);
                    var x = RandomNumberGenerator.GetInt32(1, 100);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NewRandom_NoFixOffered()
    {
        return VerifyNoFixAsync(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var bytes = RandomNumberGenerator.GetBytes(1);
                    var rng = {|E128075:new Random()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RandomSharedNextDouble_NoFixOffered()
    {
        return VerifyNoFixAsync(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var bytes = RandomNumberGenerator.GetBytes(1);
                    var x = {|E128075:Random.Shared|}.NextDouble();
                }
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("{|E128075:Random.Shared|}.Next()", "RandomNumberGenerator.GetInt32(int.MaxValue)")]
    [InlineData("{|E128075:Random.Shared|}.Next(1)", "RandomNumberGenerator.GetInt32(1)")]
    [InlineData("{|E128075:Random.Shared|}.Next(Max)", "RandomNumberGenerator.GetInt32(Max)")]
    [InlineData("{|E128075:Random.Shared|}.Next(B)", "RandomNumberGenerator.GetInt32(B)")]
    [InlineData("{|E128075:Random.Shared|}.Next(Ch)", "RandomNumberGenerator.GetInt32(Ch)")]
    [InlineData("{|E128075:Random.Shared|}.Next(5, 6)", "RandomNumberGenerator.GetInt32(5, 6)")]
    [InlineData("{|E128075:Random.Shared|}.Next(-10, -5)", "RandomNumberGenerator.GetInt32(-10, -5)")]
    [InlineData("{|E128075:Random.Shared|}.Next(-5, 5)", "RandomNumberGenerator.GetInt32(-5, 5)")]
    [InlineData("{|E128075:Random.Shared|}.Next(B, S)", "RandomNumberGenerator.GetInt32(B, S)")]
    [InlineData("{|E128075:Random.Shared|}.Next(Max, Max + 1)", "RandomNumberGenerator.GetInt32(Max, Max + 1)")]
    [InlineData("{|E128075:System.Random.Shared|}.Next(1, 7)", "RandomNumberGenerator.GetInt32(1, 7)")]
    public Task RandomSharedNext_FixedToGetInt32_ForConstantBounds(string original, string replacement)
    {
        return VerifyFixAsync(
            Source(original, "using System;"),
            Source(replacement, "using System;\nusing System.Security.Cryptography;"));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: 7)", "RandomNumberGenerator.GetInt32(7)")]
    [InlineData("{|E128075:Random.Shared|}.Next(minValue: 1, maxValue: 7)", "RandomNumberGenerator.GetInt32(1, 7)")]
    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: 7, minValue: 1)", "RandomNumberGenerator.GetInt32(1, 7)")]
    [InlineData("{|E128075:Random.Shared|}.Next(1, maxValue: 7)", "RandomNumberGenerator.GetInt32(1, 7)")]
    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: Max + 1, minValue: Max)", "RandomNumberGenerator.GetInt32(Max, Max + 1)")]
    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: B, minValue: -B)", "RandomNumberGenerator.GetInt32(-B, B)")]
    public Task RandomSharedNext_NamedArguments_FixedToGetInt32(string original, string replacement)
    {
        return VerifyFixAsync(
            Source(original, "using System;"),
            Source(replacement, "using System;\nusing System.Security.Cryptography;"));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("{|E128075:Random.Shared|}.Next(0)")]
    [InlineData("{|E128075:Random.Shared|}.Next(2, 2)")]
    [InlineData("{|E128075:Random.Shared|}.Next(5, 5)")]
    [InlineData("{|E128075:Random.Shared|}.Next(10, 2)")]
    [InlineData("{|E128075:Random.Shared|}.Next(-3)")]
    [InlineData("{|E128075:Random.Shared|}.Next(Z)")]
    [InlineData("{|E128075:Random.Shared|}.Next(B, B)")]
    [InlineData("{|E128075:Random.Shared|}.Next(Max, Max)")]
    public Task RandomSharedNext_NoFixOffered_ForEmptyOrNegativeRange(string expression)
    {
        return VerifyNoFixAsync(Source(expression, "using System;"));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("{|E128075:Random.Shared|}.Next(n)")]
    [InlineData("{|E128075:Random.Shared|}.Next(n, n)")]
    [InlineData("{|E128075:Random.Shared|}.Next(n, n + 5)")]
    [InlineData("{|E128075:Random.Shared|}.Next(1, n)")]
    [InlineData("{|E128075:Random.Shared|}.Next(n, 10)")]
    [InlineData("{|E128075:Random.Shared|}.Next(n++)")]
    [InlineData("{|E128075:Random.Shared|}.Next(Get())")]
    [InlineData("{|E128075:Random.Shared|}.Next(1, Get())")]
    [InlineData("{|E128075:Random.Shared|}.Next(Extra)")]
    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: n)")]
    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: 7, minValue: n)")]
    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: Get())")]
    public Task RandomSharedNext_NoFixOffered_ForNonConstantBounds(string expression)
    {
        return VerifyNoFixAsync(Source(expression, "using System;"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NewRandomInstanceNext_NoFixOffered()
    {
        return VerifyNoFixAsync(Source("{|E128075:new Random()|}.Next(5)", "using System;"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RandomSharedNextInt64_NoFixOffered()
    {
        return VerifyNoFixAsync(Source("{|E128075:Random.Shared|}.NextInt64(5)", "using System;"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixIsNotCapturedByLocalNamedSystem()
    {
        return VerifyFixAsync(
            """
            using System;
            class C
            {
                int M()
                {
                    var System = 1;
                    var token = {|E128075:Random.Shared|}.Next(5);
                    return System + token;
                }
            }
            """,
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                int M()
                {
                    var System = 1;
                    var token = RandomNumberGenerator.GetInt32(5);
                    return System + token;
                }
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("{|E128075:Random.Shared|}.Next(5)", "class RandomNumberGenerator { }")]
    [InlineData("{|E128075:Random.Shared|}.Next(5)", "class RandomNumberGenerator { public static int GetInt32(int toExclusive) => 0; }")]
    [InlineData("{|E128075:Random.Shared|}.Next(maxValue: 7, minValue: 1)", "class RandomNumberGenerator { public static int GetInt32(int toExclusive) => 0; }")]
    public Task ShadowedRandomNumberGenerator_NoFixOffered(string expression, string shadow)
    {
        var source = Source(expression, "using System;") +
                     "\nnamespace System.Security.Cryptography\n{\n    " + shadow + "\n}\n";
        return VerifyNoFixAsync(source);
    }

    private static string Source(string expression, string usings)
    {
        return $$"""
                 {{usings}}

                 class C
                 {
                     const byte B = 5;
                     const byte Z = 0;
                     const short S = 7;
                     const char Ch = 'a';
                     const int Max = 10;
                     static int Extra = 3;
                     static int Get() => 3;

                     int M(int n)
                     {
                         var token = {{expression}};
                         return 0;
                     }
                 }

                 """;
    }
}
