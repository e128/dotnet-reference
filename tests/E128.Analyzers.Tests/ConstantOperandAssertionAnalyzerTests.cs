using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConstantOperandAssertionAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void ConstantOperandAssertionAnalyzer_ReportsAssertion_WhenBothOperandsAreLiterals()
        => Assert.Fail("Not implemented, see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void ConstantOperandAssertionAnalyzer_ReportsNothing_WhenOneOperandIsNotConstant()
        => Assert.Fail("Not implemented, see Verifiable Behaviors in plan.md");
}
