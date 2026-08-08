using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TestCodeCommentAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void Analyzer_Reports_WhenFactBodyHasLineComment()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void Analyzer_Reports_WhenTestClassHelperHasDocComment()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void Analyzer_ReportsNothing_WhenTypeHasNoXunitAttribute()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");
}
