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

    public Task SaveAsync(Greeting greeting, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(greeting);

        lock (_gate)
        {
            _greetings.Add(greeting);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Greeting>> GetRecentAsync(int count, CancellationToken cancellationToken)
    {
        Greeting[] recent;

        lock (_gate)
        {
            recent =
            [
                .. _greetings
                    .OrderByDescending(static greeting => greeting.CreatedAt)
                    .Take(count)
            ];
        }

        return Task.FromResult<IReadOnlyList<Greeting>>(recent);
    }
}
