using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TestCodeCommentCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void CodeFix_RemovesComment_WithoutExtraBlankLines()
        => Assert.Fail("Not implemented — see Verifiable Behaviors in plan.md");
}
