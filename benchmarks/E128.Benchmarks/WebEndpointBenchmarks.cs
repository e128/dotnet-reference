using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using E128.Reference.Core.Models;
using E128.Reference.Core.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace E128.Benchmarks;

/// <summary>
///     Starter benchmarks for the REST endpoints. The app runs in-process behind the test server,
///     so the numbers exclude the network and the Kestrel socket layer.
///     A fixed repository replaces the in-memory one, so the stored state does not grow per request
///     and the numbers do not drift over a run.
///     BenchmarkDotNet requires an unsealed class, so this class is not sealed.
/// </summary>
[MemoryDiagnoser]
public class WebEndpointBenchmarks : IDisposable
{
    private const int StoredGreetings = 10;

    private static readonly Uri RootUri = new("/", UriKind.Relative);
    private static readonly Uri GreetingsUri = new("/greetings", UriKind.Relative);
    private static readonly Uri RecentGreetingsUri = new($"/greetings?count={StoredGreetings}", UriKind.Relative);

    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(static services =>
        {
            var greetings = Enumerable.Range(0, StoredGreetings)
                .Select(static i => new Greeting("Hello, Ada!", "Ada", DateTimeOffset.UnixEpoch.AddSeconds(i)))
                .ToArray();

            services.RemoveAll<IGreetingRepository>();
            services.AddSingleton<IGreetingRepository>(new FixedGreetingRepository(greetings));
        }));

    private readonly HttpClient _client;
    private readonly GreetingRequest _request = new("Ada");
    private bool _disposed;

    public WebEndpointBenchmarks()
    {
        _client = _factory.CreateClient();
    }

    [GlobalSetup]
    public async Task WarmUpAsync()
    {
        // The first request builds the request delegates and JSON metadata. Keep that cost out of the measured runs.
        using var response = await _client.PostAsJsonAsync(GreetingsUri, _request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    [GlobalCleanup]
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _client.Dispose();
            _factory.Dispose();
        }

        _disposed = true;
    }

    [Benchmark]
    public async Task<string> GetRootAsync()
    {
        using var response = await _client.GetAsync(RootUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<HttpStatusCode> PostGreetingAsync()
    {
        using var response = await _client.PostAsJsonAsync(GreetingsUri, _request).ConfigureAwait(false);
        return response.StatusCode;
    }

    [Benchmark]
    public async Task<string> GetGreetingsAsync()
    {
        using var response = await _client.GetAsync(RecentGreetingsUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }
}
