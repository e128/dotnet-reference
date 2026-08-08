using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PragmaFileScopedNamespaceAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void Analyzer_Reports_WhenPragmaPrecedesFileScopedNamespace()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void Analyzer_ReportsNothing_WhenPragmaFollowsNamespace()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void Analyzer_ReportsNothing_WhenNamespaceIsBlockScoped()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");
}
