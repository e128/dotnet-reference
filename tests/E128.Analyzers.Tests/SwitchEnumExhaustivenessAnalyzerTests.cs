using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SwitchEnumExhaustivenessAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void Analyzer_Reports_WhenEnumMemberUncased()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void Analyzer_ReportsNothing_WhenAllMembersCased()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void Analyzer_ReportsNothing_WhenDefaultArmPresent()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void Analyzer_ReportsNothing_WhenGoverningTypeIsString()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");
}
