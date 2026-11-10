using System;
using System.Threading;
using System.Threading.Tasks;
using E128.Reference.Core.Models;
using E128.Reference.Core.Repositories;

namespace E128.Reference.Core.Services;

/// <summary>
///     Default greeting service — creates greetings and persists them.
/// </summary>
public sealed class GreetingService(
    Greeter greeter,
    IGreetingRepository repository,
    TimeProvider timeProvider) : IGreetingService
{
    public Task<Greeting> GreetAsync(GreetingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return GreetCoreAsync(request, cancellationToken);
    }

    private async Task<Greeting> GreetCoreAsync(GreetingRequest request, CancellationToken cancellationToken)
    {
        var message = greeter.Greet(request.Name);
        var greeting = new Greeting(message, request.Name ?? greeter.DefaultName, timeProvider.GetUtcNow());

        await repository.SaveAsync(greeting, cancellationToken).ConfigureAwait(false);

        return greeting;
    }
}
