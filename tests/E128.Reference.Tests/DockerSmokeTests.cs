using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace E128.Reference.Tests;

public sealed class DockerSmokeTests : IAsyncLifetime
{
    private const string ImageName = "e128-reference-web-test";
    private const string ContainerName = "e128-reference-web-smoke";
    private const int HostPort = 58080;
    private readonly HttpClient _client = new() { BaseAddress = new Uri($"http://localhost:{HostPort}") };

    public async ValueTask InitializeAsync()
    {
        await RunProcessAsync("docker", $"build -t {ImageName} .");
        await RunProcessAsync("docker", $"run -d --name {ContainerName} -p {HostPort}:8080 {ImageName}");
        await WaitForHealthy();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await RunProcessAsync("docker", $"rm -f {ContainerName}");
        await RunProcessAsync("docker", $"rmi -f {ImageName}");
    }

    [Fact]
    [Trait("Category", "Docker")]
    public async Task Root_ReturnsGreeting()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Hello, World!", content);
    }

    [Fact]
    [Trait("Category", "Docker")]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("healthy", doc.RootElement.GetProperty("status").GetString());
    }

    private async Task WaitForHealthy()
    {
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var response = await _client.GetAsync("/health", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Container not ready yet
            }

            await Task.Delay(500, cts.Token);
        }

        throw new TimeoutException("Container did not become healthy within 30 seconds");
    }

    private static async Task RunProcessAsync(string fileName, string arguments)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        process.Start();
        await process.WaitForExitAsync();
    }
}
