using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InsecureRandomInCryptoContextBranchCoverageGapTests
{
    private static Task VerifyAsync(string code, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<InsecureRandomInCryptoContextAnalyzer>(
            code,
            expected: null,
            options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenANextBytesOverloadTakesNoBuffer()
    {
        return VerifyAsync("""
            using System;
            sealed class FixedRandom : Random
            {
                public void NextBytes() { }
            }

            class C
            {
                private readonly FixedRandom _random = new();

                void M()
                {
                    _random.NextBytes();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheValueReachesASensitiveNameThroughAPropertyRead()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                void M()
                {
                    var tokenLength = {|E128075:new Random()|}.Next().ToString().Length;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheValueReachesASensitiveNameThroughAFieldRead()
    {
        return VerifyAsync("""
            using System;
            struct Pair
            {
                public int Value;
            }

            sealed class PairRandom : Random
            {
                public Pair NextPair() => default;
            }

            class C
            {
                void M()
                {
                    var tokenValue = {|E128075:new PairRandom()|}.NextPair().Value;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheValueIsOnlyTheReceiverOfAnAssignmentTarget()
    {
        return VerifyAsync("""
            using System;
            class Holder
            {
                public int Token { get; set; }
            }

            sealed class HolderRandom : Random
            {
                public Holder NextHolder() => new();
            }

            class C
            {
                void M()
                {
                    new HolderRandom().NextHolder().Token = 1;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenARandomValueIsAssignedToASensitiveProperty()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                int Token { get; set; }

                void M()
                {
                    Token = {|E128075:new Random()|}.Next();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenACryptographyNameDoesNotBind()
    {
        return VerifyAsync(
            """
            using System;
            class C
            {
                void M()
                {
                    var number = new Random().Next();
                    RandomNumberGenerator.Fill(null);
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Handles_ARandomValuePassedToCallsThatReturnAStringOrNothing()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                static string Format(int value) => value.ToString();

                static void Consume(int value) { }

                void M(Random random)
                {
                    var tokenText = Format({|E128075:random.Next()|});
                    Consume(random.Next());
                }
            }
            """);
    }
}
