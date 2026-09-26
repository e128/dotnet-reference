using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ParallelCollectionIndexAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void ParallelCollectionIndexAnalyzer_ReportsIndex_WhenLengthGuardIsAbsent()
        => Assert.Fail("Not implemented, see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void ParallelCollectionIndexAnalyzer_ReportsNothing_WhenLengthGuardIsPresent()
        => Assert.Fail("Not implemented, see Verifiable Behaviors in plan.md");
}
