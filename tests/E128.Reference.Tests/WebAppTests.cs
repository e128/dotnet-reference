using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using E128.Reference.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace E128.Reference.Tests;

public sealed class WebAppTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client = factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Root_ReturnsOk_WithGreeting()
    {
        using var response = await _client.GetAsync(new Uri("/", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Hello, World!", content);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Greetings_ReturnsOk_WithSourceGeneratedJson()
    {
        using var created = await _client.PostAsJsonAsync("/greetings", new GreetingRequest("Ada"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var response = await _client.GetAsync(new Uri("/greetings?count=1", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"recipientName\":\"Ada\"", content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A blank name is the expected failure the result union carries, so the endpoint answers
    ///     with the failure case instead of creating a greeting.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public async Task Greetings_ReturnsBadRequest_WhenNameIsBlank()
    {
        using var response = await _client.PostAsJsonAsync("/greetings", new GreetingRequest(" "), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // ASP.NET also answers 400 when the body fails to bind, so assert the reason
        // too. A broken request shape would otherwise pass this test.
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("A recipient name cannot be blank.", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Health_ReturnsOk()
    {
        using var response = await _client.GetAsync(new Uri("/health", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
