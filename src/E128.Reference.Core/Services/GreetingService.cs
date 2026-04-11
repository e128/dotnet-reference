using System;
using System.Threading;
using System.Threading.Tasks;
using E128.Reference.Core.Models;
using E128.Reference.Core.Repositories;

namespace E128.Reference.Core.Services;

/// <summary>
/// Default greeting service — creates greetings and persists them.
/// </summary>
public sealed class GreetingService(
    Greeter greeter,
    IGreetingRepository repository,
    TimeProvider timeProvider) : IGreetingService
{
    public async Task<Greeting> GreetAsync(GreetingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var message = greeter.Greet(request.Name);
        var greeting = new Greeting(message, request.Name ?? "World", timeProvider.GetUtcNow());

        await repository.SaveAsync(greeting, cancellationToken).ConfigureAwait(false);

        return greeting;
    }
}
