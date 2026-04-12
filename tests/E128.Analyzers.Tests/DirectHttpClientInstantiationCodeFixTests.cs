using System.Threading.Tasks;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DirectHttpClientInstantiationCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesNewHttpClient_WithFactoryCreateClient()
    {
        Assert.Fail("Not implemented — E128004 fix replaces new HttpClient() with httpClientFactory.CreateClient()");
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReusesExistingFactory_WhenFieldExists()
    {
        Assert.Fail("Not implemented — E128004 fix detects existing IHttpClientFactory field and reuses it");
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsConstructorParameter_WhenNoFactory()
    {
        Assert.Fail("Not implemented — E128004 fix adds IHttpClientFactory as constructor param and field when missing");
        return Task.CompletedTask;
    }
}
