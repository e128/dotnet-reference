using System;
using System.Globalization;
using System.Threading;
using E128.Reference.Core;
using E128.Reference.Core.Models;
using E128.Reference.Core.Repositories;
using E128.Reference.Core.Services;
using E128.Reference.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Greeter>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IGreetingRepository, InMemoryGreetingRepository>();
builder.Services.AddSingleton<IGreetingService, GreetingService>();
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(static options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

var app = builder.Build();

app.MapGet("/", (Greeter greeter) => greeter.Greet());

app.MapPost("/greetings", async (GreetingRequest request, IGreetingService service, CancellationToken cancellationToken) =>
{
    var greeting = await service.GreetAsync(request, cancellationToken);
    return Results.Created(string.Create(CultureInfo.InvariantCulture, $"/greetings/{greeting.CreatedAt.Ticks}"), greeting);
});

app.MapGet("/greetings", async (IGreetingRepository repository, int? count, CancellationToken cancellationToken) =>
{
    var greetings = await repository.GetRecentAsync(count ?? 10, cancellationToken);
    return Results.Ok(greetings);
});

app.MapHealthChecks("/health");

await app.RunAsync();
