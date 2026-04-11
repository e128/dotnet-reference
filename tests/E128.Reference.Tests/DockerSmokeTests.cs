using System;
using System.IO;
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

    private static readonly string RepoRoot = FindRepoRoot();

    private readonly HttpClient _client = new() { BaseAddress = new Uri($"http://localhost:{HostPort}") };

    public async ValueTask InitializeAsync()
    {
        await RunDockerAsync($"build --tag {ImageName} .");
        await RunDockerAsync($"run -d --name {ContainerName} -p {HostPort}:8080 {ImageName}");
        await WaitForHealthy();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await RunDockerAsync($"rm -f {ContainerName}", throwOnError: false);
        await RunDockerAsync($"rmi -f {ImageName}", throwOnError: false);
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
            catch (TaskCanceledException) when (cts.Token.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(500, cts.Token);
            }
            catch (TaskCanceledException) when (cts.Token.IsCancellationRequested)
            {
                break;
            }
        }

        throw new TimeoutException("Container did not become healthy within 30 seconds");
    }

    /// <summary>
    /// Runs a docker command with the repo root as the working directory.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private static async Task RunDockerAsync(string arguments, bool throwOnError = true)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        process.Start();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (throwOnError && process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'docker {arguments}' failed (exit {process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)}): {stderr}");
        }
    }

    /// <summary>
    /// Walks up from the test output directory to find the repo root (contains Dockerfile).
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Dockerfile")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not find repo root containing Dockerfile");
    }
}
