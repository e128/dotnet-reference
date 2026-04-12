using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DateTimeDirectUseCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void CodeFix_ReplacesDateTimeUtcNow_WithTimeProviderSystem()
    {
        Assert.Fail("Not implemented — see acceptance criteria AC1 in context.md");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void CodeFix_ReplacesDateTimeNow_WithTimeProviderSystemGetLocalNow()
    {
        Assert.Fail("Not implemented — see acceptance criteria AC2 in context.md");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void CodeFix_ReplacesDateTimeToday_WithTimeProviderSystemGetLocalNowDate()
    {
        Assert.Fail("Not implemented — see acceptance criteria AC2 in context.md");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void CodeFix_ReplacesDateTimeOffsetNow_WithTimeProviderSystemGetLocalNow()
    {
        Assert.Fail("Not implemented — see acceptance criteria AC2 in context.md");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void CodeFix_ReplacesDateTimeOffsetUtcNow_WithTimeProviderSystemGetUtcNow()
    {
        Assert.Fail("Not implemented — see acceptance criteria AC2 in context.md");
    }
}
