using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using E128.Reference.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace E128.Reference.Tests;

public sealed class WebAppTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    [Trait("Category", "CI")]
    public async Task Root_ReturnsOk_WithGreeting()
    {
        var response = await _client.GetAsync("/", HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Hello, World!", content);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Greetings_ReturnsOk_WithSourceGeneratedJson()
    {
        using var created = await _client.PostAsJsonAsync("/greetings", new GreetingRequest("Ada"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var response = await _client.GetAsync("/greetings?count=1", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"recipientName\":\"Ada\"", content, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health", HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
