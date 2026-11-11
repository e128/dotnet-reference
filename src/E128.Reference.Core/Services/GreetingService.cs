using System;
using System.Threading;
using System.Threading.Tasks;
using E128.Reference.Core.Models;
using E128.Reference.Core.Repositories;

namespace E128.Reference.Core.Services;

/// <summary>
///     Default greeting service. Creates greetings and persists them.
/// </summary>
public sealed class GreetingService(
    Greeter greeter,
    IGreetingRepository repository,
    TimeProvider timeProvider) : IGreetingService
{
    /// <inheritdoc />
    public Task<GreetingResult> GreetAsync(GreetingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return GreetCoreAsync(request, cancellationToken);
    }

    private async Task<GreetingResult> GreetCoreAsync(GreetingRequest request, CancellationToken cancellationToken)
    {
        // A null name means "use the default". A present but blank name is an expected failure.
        if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
        {
            return new GreetingError("A recipient name cannot be blank.");
        }

        var message = greeter.Greet(request.Name);
        var greeting = new Greeting(message, request.Name ?? greeter.DefaultName, timeProvider.GetUtcNow());

        await repository.SaveAsync(greeting, cancellationToken).ConfigureAwait(false);

        return greeting;
    }
}
