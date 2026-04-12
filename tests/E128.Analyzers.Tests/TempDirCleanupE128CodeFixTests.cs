using System.Threading.Tasks;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TempDirCleanupE128CodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task TempDirCleanup_CodeFix_AddsIAsyncLifetime()
    {
        Assert.Fail("AC #7: code fix should add IAsyncLifetime with DisposeAsync");
        return Task.CompletedTask;
    }
}
