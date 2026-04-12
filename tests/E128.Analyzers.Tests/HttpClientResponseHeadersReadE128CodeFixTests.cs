using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientResponseHeadersReadE128CodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void CodeFix_InsertsResponseHeadersRead_ForGetAsyncCall()
    {
        Assert.Fail("Not implemented — see acceptance criteria AC8 in context.md");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void CodeFix_InsertsResponseHeadersRead_ForSendAsyncCall()
    {
        Assert.Fail("Not implemented — see acceptance criteria AC8 in context.md");
    }
}
