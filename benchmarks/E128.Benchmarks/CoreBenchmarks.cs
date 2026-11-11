using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using E128.Reference.Core;
using E128.Reference.Core.Models;
using E128.Reference.Core.Repositories;
using E128.Reference.Core.Services;

namespace E128.Benchmarks;

/// <summary>
///     Starter benchmarks for the public types in E128.Reference.Core.
///     BenchmarkDotNet requires an unsealed class, so this class is not sealed.
/// </summary>
[MemoryDiagnoser]
public class CoreBenchmarks
{
    private const int SeededGreetings = 100;
    private const int RecentCount = 10;

    private readonly Greeter _greeter = new();
    private readonly InMemoryGreetingRepository _repository = new();
    private readonly GreetingService _service;
    private readonly GreetingRequest _request = new("Ada");

    public CoreBenchmarks()
    {
        // The service benchmark uses a repository that stores nothing, so the list does not grow per operation.
        _service = new GreetingService(_greeter, new FixedGreetingRepository([]), TimeProvider.System);
    }

    [GlobalSetup]
    public async Task SeedRepositoryAsync()
    {
        for (var i = 0; i < SeededGreetings; i++)
        {
            var greeting = new Greeting("Hello, Ada!", "Ada", DateTimeOffset.UnixEpoch.AddSeconds(i));
            await _repository.SaveAsync(greeting, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Benchmark]
    public string GreetDefault()
    {
        return _greeter.Greet();
    }

    [Benchmark]
    public string GreetNamed()
    {
        return _greeter.Greet("Ada");
    }

    [Benchmark]
    public Task<GreetingResult> GreetAndSaveAsync()
    {
        return _service.GreetAsync(_request, CancellationToken.None);
    }

    [Benchmark]
    public Task<IReadOnlyList<Greeting>> GetRecentTenAsync()
    {
        return _repository.GetRecentAsync(RecentCount, CancellationToken.None);
    }
}
