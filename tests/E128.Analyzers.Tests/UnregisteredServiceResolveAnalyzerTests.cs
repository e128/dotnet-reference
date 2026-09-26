using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnregisteredServiceResolveAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void UnregisteredServiceResolveAnalyzer_ReportsResolve_WhenNoRegistrationExists()
        => Assert.Fail("Not implemented, see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void UnregisteredServiceResolveAnalyzer_ReportsNothing_WhenServiceIsFrameworkProvided()
        => Assert.Fail("Not implemented, see Verifiable Behaviors in plan.md");
}
