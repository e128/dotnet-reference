using System;
using System.Collections.Generic;
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
        // Recency follows save order. GreetingService stamps CreatedAt at save time,
        // so save order and timestamp order agree.
        Greeting[] recent;

        lock (_gate)
        {
            var take = Math.Clamp(count, 0, _greetings.Count);
            recent = new Greeting[take];

            for (var i = 0; i < take; i++)
            {
                recent[i] = _greetings[_greetings.Count - 1 - i];
            }
        }

        return Task.FromResult<IReadOnlyList<Greeting>>(recent);
    }
}
