using System.Threading;
using System.Threading.Tasks;
using E128.Reference.Core.Models;

namespace E128.Reference.Core.Services;

/// <summary>
///     Application service for greeting operations.
/// </summary>
public interface IGreetingService
{
    /// <summary>
    ///     Creates a greeting and persists it. An expected failure returns the failure case.
    /// </summary>
    /// <param name="request">The inbound request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The greeting, or the expected failure.</returns>
    Task<GreetingResult> GreetAsync(GreetingRequest request, CancellationToken cancellationToken);
}
