using System;
using System.Threading;
using E128.Reference.Core;
using E128.Reference.Core.Models;
using E128.Reference.Core.Repositories;
using E128.Reference.Core.Services;
using E128.Reference.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// The per-request Information logs cost a console write and a lock per request.
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSingleton<Greeter>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IGreetingRepository, InMemoryGreetingRepository>();
builder.Services.AddSingleton<IGreetingService, GreetingService>();
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(static options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

var app = builder.Build();

app.MapGet("/", static (Greeter greeter) => greeter.Greet());

app.MapPost("/greetings", static async (GreetingRequest request, IGreetingService service, CancellationToken cancellationToken) =>
{
    var result = await service.GreetAsync(request, cancellationToken);

    return result.Match(static value => value switch
    {
        Greeting greeting => Results.Created(new Uri("/greetings", UriKind.Relative), greeting),
        GreetingError error => Results.BadRequest(error.Message),
        _ => Results.Problem("Unknown greeting result.")
    });
});

app.MapGet("/greetings", static async (IGreetingRepository repository, int? count, CancellationToken cancellationToken) =>
{
    var greetings = await repository.GetRecentAsync(count ?? 10, cancellationToken);
    return Results.Ok(greetings);
});

app.MapHealthChecks("/health");

await app.RunAsync();
