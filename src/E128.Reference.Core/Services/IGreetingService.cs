using System.Threading;
using System.Threading.Tasks;
using E128.Reference.Core.Models;

namespace E128.Reference.Core.Services;

/// <summary>
/// Application service for greeting operations.
/// </summary>
public interface IGreetingService
{
    Task<Greeting> GreetAsync(GreetingRequest request, CancellationToken cancellationToken = default);
}
