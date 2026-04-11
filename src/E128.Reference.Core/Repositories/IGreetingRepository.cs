using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using E128.Reference.Core.Models;

namespace E128.Reference.Core.Repositories;

/// <summary>
/// Persistence abstraction for greetings.
/// </summary>
public interface IGreetingRepository
{
    Task SaveAsync(Greeting greeting, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Greeting>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
