using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using E128.Reference.Core.Models;

namespace E128.Reference.Core.Repositories;

/// <summary>
///     Persistence abstraction for greetings.
/// </summary>
public interface IGreetingRepository
{
    ValueTask SaveAsync(Greeting greeting, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<Greeting>> GetRecentAsync(int count, CancellationToken cancellationToken);
}
