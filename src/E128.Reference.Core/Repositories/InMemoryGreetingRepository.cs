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

    public ValueTask SaveAsync(Greeting greeting, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(greeting);

        lock (_gate)
        {
            _greetings.Add(greeting);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<Greeting>> GetRecentAsync(int count, CancellationToken cancellationToken)
    {
        Greeting[] recent;

        lock (_gate)
        {
            var greetings = _greetings;
            var last = greetings.Count - 1;
            var take = Math.Clamp(count, 0, last + 1);
            recent = new Greeting[take];

            for (var i = 0; i < take; i++)
            {
                recent[i] = greetings[last - i];
            }
        }

        return ValueTask.FromResult<IReadOnlyList<Greeting>>(recent);
    }
}
