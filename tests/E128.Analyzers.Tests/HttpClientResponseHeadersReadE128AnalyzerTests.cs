using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientResponseHeadersReadE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void GetAsyncCall_WithoutCompletionOption_Fires_E128010()
    {
        Assert.Fail("Not implemented — see acceptance criteria AC7 in context.md");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void SendAsyncCall_WithoutCompletionOption_Fires_E128010()
    {
        Assert.Fail("Not implemented — see acceptance criteria AC7 in context.md");
    }
}
