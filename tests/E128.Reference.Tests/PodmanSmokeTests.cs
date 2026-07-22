using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace E128.Reference.Tests;

public sealed class PodmanSmokeTests : IAsyncLifetime, IDisposable
{
    private const string ImageName = "e128-reference-web-test";
    private const string ContainerName = "e128-reference-web-smoke";
    private const int HostPort = 58080;

    private static readonly string RepoRoot = FindRepoRoot();

    private readonly HttpClient _client = new() { BaseAddress = new Uri($"http://localhost:{HostPort}") };
    private bool _podmanAvailable;

    public async ValueTask InitializeAsync()
    {
        _podmanAvailable = await IsPodmanAvailableAsync();
        if (!_podmanAvailable)
        {
            return;
        }

        await RunPodmanAsync($"build --tag {ImageName} .");
        await RunPodmanAsync($"run -d --name {ContainerName} -p {HostPort}:8080 {ImageName}");
        await WaitForHealthy();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        if (_podmanAvailable)
        {
            await RunPodmanAsync($"rm -f {ContainerName}", false);
            await RunPodmanAsync($"rmi -f {ImageName}", false);
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    [Trait("Category", "Podman")]
    public async Task Root_ReturnsGreeting()
    {
        if (!_podmanAvailable)
        {
            Assert.Skip("Podman not available");
        }

        var response = await _client.GetAsync("/", HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Hello, World!", content);
    }

    [Fact]
    [Trait("Category", "Podman")]
    public async Task Health_ReturnsHealthy()
    {
        if (!_podmanAvailable)
        {
            Assert.Skip("Podman not available");
        }

        var response = await _client.GetAsync("/health", HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", content);
    }

    private async Task WaitForHealthy()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var response = await _client.GetAsync("/health", HttpCompletionOption.ResponseHeadersRead, cts.Token);
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

    private static async Task<bool> IsPodmanAvailableAsync()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "podman",
                ArgumentList = { "info" },
                WorkingDirectory = RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Runs a podman command with the repo root as the working directory.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private static async Task RunPodmanAsync(string arguments, bool throwOnError = true)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "podman",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var token in arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            process.StartInfo.ArgumentList.Add(token);
        }

        process.Start();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (throwOnError && process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'podman {arguments}' failed (exit {process.ExitCode.ToString(CultureInfo.InvariantCulture)}): {stderr}");
        }
    }

    /// <summary>
    ///     Walks up from the test output directory to find the repo root (contains Dockerfile).
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
