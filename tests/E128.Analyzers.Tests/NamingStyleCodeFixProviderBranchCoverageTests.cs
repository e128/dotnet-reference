using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NamingStyleCodeFixProviderBranchCoverageTests
{
    private static Diagnostic CreateDiagnostic(params (string Key, string? Value)[] properties)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            builder[key] = value;
        }

        return Diagnostic.Create(
            new DiagnosticDescriptor("IDE1006", "Test", "Test", "Style", DiagnosticSeverity.Warning, true),
            Location.None,
            builder.ToImmutable());
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("max_retry_count", "", "", "_", "CamelCase", "max_Retry_Count")]
    [InlineData("max_retry_count", "", "", "_", "PascalCase", "Max_Retry_Count")]
    [InlineData("max_retry_count", "", "", "_", "FirstUpper", "Max_retry_count")]
    [InlineData("maxRetryCount", "", "", "", "FirstUpper", "MaxRetryCount")]
    [InlineData("maxRetryCount", "", "", "", "AllUpper", "MAXRETRYCOUNT")]
    [InlineData("MaxRetryCount", "", "", "_", "AllLower", "max_retry_count")]
    [InlineData("MaxRetryCount", "", "", "", "AllLowerCase", "maxretrycount")]
    [InlineData("MaxRetryCount", "", "", "-", "AllUpperCase", "MAX-RETRY-COUNT")]
    [InlineData("max_retry", "", "", "_", "Unknown", "Max_Retry")]
    public void BuildCompliantName_AppliesEachCapitalizationScheme(
        string symbolName,
        string prefix,
        string suffix,
        string separator,
        string scheme,
        string expected)
    {
        Assert.Equal(expected, NamingStyleCodeFixProvider.BuildCompliantName(symbolName, prefix, suffix, separator, scheme));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("fooAsync", "", "Async", "PascalCase", "FooAsync")]
    [InlineData("foo", "", "Async", "PascalCase", "FooAsync")]
    [InlineData("_", "_", "", "PascalCase", "_")]
    [InlineData("_", "_", "", "CamelCase", "_")]
    [InlineData("s_value", "_", "", "CamelCase", "_value")]
    [InlineData("m_value", "_", "", "CamelCase", "_value")]
    [InlineData("TValue", "", "", "PascalCase", "Value")]
    [InlineData("_Value", "s_", "", "CamelCase", "s_value")]
    public void BuildCompliantName_StripsAffixesBeforeRebuilding(
        string symbolName,
        string prefix,
        string suffix,
        string scheme,
        string expected)
    {
        Assert.Equal(expected, NamingStyleCodeFixProvider.BuildCompliantName(symbolName, prefix, suffix, string.Empty, scheme));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void SplitByCaseTransition_ReturnsNoWordsForAnEmptyName()
    {
        Assert.Empty(NamingStyleCodeFixProvider.SplitByCaseTransition(string.Empty));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ComputeCompliantName_ReturnsNullWithoutAnyName()
    {
        Assert.Null(NamingStyleCodeFixProvider.ComputeCompliantName(CreateDiagnostic(("Prefix", "_"))));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ComputeCompliantName_ReturnsNullForAnEmptySymbolName()
    {
        Assert.Null(NamingStyleCodeFixProvider.ComputeCompliantName(CreateDiagnostic(("SymbolName", string.Empty))));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ComputeCompliantName_UsesPascalCaseWhenNoStyleIsGiven()
    {
        var diagnostic = CreateDiagnostic(("SymbolName", "myField"));
        Assert.Equal("MyField", NamingStyleCodeFixProvider.ComputeCompliantName(diagnostic));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ComputeCompliantName_PrefersTheFallbackSymbolName()
    {
        var diagnostic = CreateDiagnostic(("SymbolName", "ignored"), ("Prefix", "_"), ("CapitalizationScheme", "CamelCase"));
        Assert.Equal("_actual", NamingStyleCodeFixProvider.ComputeCompliantName(diagnostic, "Actual"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ComputeCompliantName_IgnoresAnEmptySuggestedName()
    {
        var diagnostic = CreateDiagnostic(("SuggestedName", string.Empty), ("SymbolName", "myField"), ("Suffix", "Value"));
        Assert.Equal("MyFieldValue", NamingStyleCodeFixProvider.ComputeCompliantName(diagnostic));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ComputeCompliantName_ReadsEveryStyleProperty()
    {
        var properties = new List<(string Key, string? Value)>
        {
            ("SymbolName", "maxRetry"),
            ("Prefix", "k_"),
            ("Suffix", "_v"),
            ("WordSeparator", "_"),
            ("CapitalizationScheme", "AllUpperCase")
        };

        var diagnostic = CreateDiagnostic([.. properties]);
        Assert.Equal("k_MAX_RETRY_v", NamingStyleCodeFixProvider.ComputeCompliantName(diagnostic));
    }
}
