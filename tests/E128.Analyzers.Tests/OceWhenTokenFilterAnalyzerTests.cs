using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OceWhenTokenFilterAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void Reports_NegatedTokenFilter()
    {
        Assert.Fail("AC-11: catch (OperationCanceledException) when (!token.IsCancellationRequested) must be flagged");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void NoReport_PositiveTokenIdiom()
    {
        Assert.Fail("AC-12: catch (OperationCanceledException) when (token.IsCancellationRequested) must not be flagged");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void NoReport_UnfilteredCatch()
    {
        Assert.Fail("AC-13: catch (OperationCanceledException) with no filter must not be flagged");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void NoReport_UnrelatedFilter()
    {
        Assert.Fail("AC-14: a filter that does not reference IsCancellationRequested must not be flagged");
    }
}
