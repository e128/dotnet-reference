using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SyncOverAsyncCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void CodeFix_ReplacesTaskResult_WithAwait()
    {
        Assert.Fail("Not implemented — see acceptance criteria AC3 in context.md");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void CodeFix_ReplacesGetAwaiterGetResult_WithAwait()
    {
        Assert.Fail("Not implemented — see acceptance criteria AC4 in context.md");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void CodeFix_PromotesSyncMethodToAsync_ChangesReturnType()
    {
        Assert.Fail("Not implemented — see acceptance criteria AC4 in context.md");
    }
}
