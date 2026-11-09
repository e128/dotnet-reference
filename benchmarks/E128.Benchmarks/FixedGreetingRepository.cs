using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using E128.Reference.Core.Models;
using E128.Reference.Core.Repositories;

namespace E128.Benchmarks;

/// <summary>
///     A repository whose state never changes. Save discards the greeting and read returns the same list,
///     so benchmark cost stays constant however many iterations run.
/// </summary>
internal sealed class FixedGreetingRepository(IReadOnlyList<Greeting> greetings) : IGreetingRepository
{
    public Task SaveAsync(Greeting greeting, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(greeting);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Greeting>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return Task.FromResult(greetings);
    }
}
