using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ProcessOutputValidatorAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void ProcessOutputValidatorAnalyzer_FlagsOutputUse_WhenNoExistenceCheck()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void ProcessOutputValidatorAnalyzer_DoesNotFlag_WhenExistenceCheckPresent()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");

    [Fact]
    [Trait("Category", "CI")]
    public void ProcessOutputValidatorAnalyzer_DoesNotFlag_WhenAsyncWaitHasCheck()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");
}
