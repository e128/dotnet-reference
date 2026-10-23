using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E128.Reference.Core.Models;

namespace E128.Reference.Core.Repositories;

/// <summary>
///     In-memory implementation for development and testing. Thread-safe, because the
///     composition root registers it as a singleton.
/// </summary>
public sealed class InMemoryGreetingRepository : IGreetingRepository
{
    private readonly Lock _gate = new();
    private readonly List<Greeting> _greetings = [];

    public Task SaveAsync(Greeting greeting, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(greeting);

        lock (_gate)
        {
            _greetings.Add(greeting);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Greeting>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        Greeting[] snapshot;

        lock (_gate)
        {
            snapshot = [.. _greetings];
        }

        IReadOnlyList<Greeting> result =
        [
            .. snapshot
                .OrderByDescending(g => g.CreatedAt)
                .Take(count)
        ];

        return Task.FromResult(result);
    }
}
