using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MultiStringEqualsOrChainAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<MultiStringEqualsOrChainAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    #region Fires — string.Equals with StringComparison

    [Fact]
    [Trait("Category", "CI")]
    public Task StringEquals_ThreeChain_OrdinalIgnoreCase_Fires()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                bool M(string text) =>
                    {|E128029:string.Equals(text, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "b", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "c", StringComparison.OrdinalIgnoreCase)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringEquals_ThreeChain_Ordinal_Fires()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                bool M(string text) =>
                    {|E128029:string.Equals(text, "a", StringComparison.Ordinal) || string.Equals(text, "b", StringComparison.Ordinal) || string.Equals(text, "c", StringComparison.Ordinal)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EqualityOperator_ThreeChain_Fires()
    {
        return VerifyAsync("""
            class C
            {
                bool M(string text) =>
                    {|E128029:text == "a" || text == "b" || text == "c"|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringEquals_FourChain_SingleDiagnostic_Fires()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                bool M(string text) =>
                    {|E128029:string.Equals(text, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "b", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "c", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "d", StringComparison.OrdinalIgnoreCase)|};
            }
            """);
    }

    #endregion Fires — string.Equals with StringComparison

    #region No-fire cases

    [Fact]
    [Trait("Category", "CI")]
    public Task StringEquals_TwoChain_BelowThreshold_NoFire()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                bool M(string text) =>
                    string.Equals(text, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "b", StringComparison.OrdinalIgnoreCase);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringEquals_MixedOperands_NoFire()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                bool M(string text, string other) =>
                    string.Equals(text, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(other, "b", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "c", StringComparison.OrdinalIgnoreCase);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringEquals_MixedComparison_NoFire()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                bool M(string text) =>
                    string.Equals(text, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "b", StringComparison.Ordinal) || string.Equals(text, "c", StringComparison.OrdinalIgnoreCase);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AndOperator_NoFire()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                bool M(string text) =>
                    string.Equals(text, "a", StringComparison.OrdinalIgnoreCase) && string.Equals(text, "b", StringComparison.OrdinalIgnoreCase) && string.Equals(text, "c", StringComparison.OrdinalIgnoreCase);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EqualityOperator_LiteralOnLeft_NoFire()
    {
        return VerifyAsync("""
            class C
            {
                bool M(string text) =>
                    "a" == text || "b" == text || "c" == text;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EqualityOperator_IntLiterals_NoFire()
    {
        return VerifyAsync("""
            class C
            {
                bool M(int n) =>
                    n == 1 || n == 2 || n == 3;
            }
            """);
    }

    #endregion No-fire cases

    #region Mixed OR-chain — qualifying sub-group detected

    [Fact]
    [Trait("Category", "CI")]
    public Task MixedHead_ThreeQualifyingLeaves_Fires()
    {
        // Non-qualifying leaf at the head; qualifying sub-group of 3 follows.
        return VerifyAsync("""
            using System;
            class C
            {
                bool M(string href, string text) =>
                    {|E128029:href.Contains("x") || string.Equals(text, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "b", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "c", StringComparison.OrdinalIgnoreCase)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MixedTail_ThreeQualifyingLeaves_Fires()
    {
        // Non-qualifying leaf at the tail; qualifying sub-group of 3 precedes.
        return VerifyAsync("""
            using System;
            class C
            {
                bool M(string href, string text) =>
                    {|E128029:string.Equals(text, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "b", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "c", StringComparison.OrdinalIgnoreCase) || href.Contains("x")|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MixedHead_TwoQualifyingLeaves_NoFire()
    {
        // Non-qualifying leaf + only 2 qualifying — below threshold, no fire.
        return VerifyAsync("""
            using System;
            class C
            {
                bool M(string href, string text) =>
                    href.Contains("x") || string.Equals(text, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "b", StringComparison.OrdinalIgnoreCase);
            }
            """);
    }

    #endregion Mixed OR-chain — qualifying sub-group detected
}
