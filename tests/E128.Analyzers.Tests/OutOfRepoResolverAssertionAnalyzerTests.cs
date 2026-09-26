using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OutOfRepoResolverAssertionAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void OutOfRepoResolverAssertionAnalyzer_ReportsTest_WhenSkipGuardIsAbsent()
        => Assert.Fail("Not implemented, see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void OutOfRepoResolverAssertionAnalyzer_ReportsNothing_WhenSkipGuardIsPresent()
        => Assert.Fail("Not implemented, see Verifiable Behaviors in plan.md");
}
