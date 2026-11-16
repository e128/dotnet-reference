using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MultiStringEqualsOrChainAnalyzerBranchCoverageGapTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStringEqualsCallsDoNotResolve()
    {
        return AnalyzerTestRunner.VerifyAsync<MultiStringEqualsOrChainAnalyzer>(
            """
            class C
            {
                bool M(string text) =>
                    string.Equals(text, "a", 1) || string.Equals(text, "b", 2) || string.Equals(text, "c", 3);
            }
            """,
            expected: null,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheComparedValueIsNotALiteral()
    {
        return AnalyzerTestRunner.VerifyAsync<MultiStringEqualsOrChainAnalyzer>("""
            using System;
            class C
            {
                bool M(string text, string a, string b, string c) =>
                    string.Equals(text, a, StringComparison.Ordinal)
                    || string.Equals(text, b, StringComparison.Ordinal)
                    || string.Equals(text, c, StringComparison.Ordinal);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void StringEqualityInfo_ComparesByEveryField()
    {
        var baseline = new MultiStringEqualsOrChainAnalyzer.StringEqualityInfo("text", "a", "==");
        var same = new MultiStringEqualsOrChainAnalyzer.StringEqualityInfo("text", "a", "==");

        Assert.True(baseline.Equals(same));
        Assert.True(baseline.Equals((object)same));
        Assert.Equal(baseline.GetHashCode(), same.GetHashCode());
        Assert.False(baseline.Equals("text"));
        Assert.False(baseline.Equals(new MultiStringEqualsOrChainAnalyzer.StringEqualityInfo("other", "a", "==")));
        Assert.False(baseline.Equals(new MultiStringEqualsOrChainAnalyzer.StringEqualityInfo("text", "b", "==")));
        Assert.False(baseline.Equals(new MultiStringEqualsOrChainAnalyzer.StringEqualityInfo("text", "a", "Ordinal")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void StringEqualityInfo_HandlesAnEmptyValue()
    {
        var empty = default(MultiStringEqualsOrChainAnalyzer.StringEqualityInfo);

        Assert.Equal(empty.GetHashCode(), default(MultiStringEqualsOrChainAnalyzer.StringEqualityInfo).GetHashCode());
        Assert.NotEqual(
            empty.GetHashCode(),
            new MultiStringEqualsOrChainAnalyzer.StringEqualityInfo("text", "a", "==").GetHashCode());
        Assert.Equal("(, , )", empty.ToString());
    }

    [Fact]
    [Trait("Category", "CI")]
    public void StringEqualityInfo_ToString_ListsEveryField()
    {
        var info = new MultiStringEqualsOrChainAnalyzer.StringEqualityInfo("text", "a", "==");

        Assert.Equal("(text, a, ==)", info.ToString());
    }
}
