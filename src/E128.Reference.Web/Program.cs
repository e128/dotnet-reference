using System;
using System.Globalization;
using E128.Reference.Core;
using E128.Reference.Core.Models;
using E128.Reference.Core.Repositories;
using E128.Reference.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Greeter>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IGreetingRepository, InMemoryGreetingRepository>();
builder.Services.AddSingleton<IGreetingService, GreetingService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", (Greeter greeter) => greeter.Greet());

app.MapPost("/greetings", async (GreetingRequest request, IGreetingService service) =>
{
    var greeting = await service.GreetAsync(request);
    return Results.Created(string.Create(CultureInfo.InvariantCulture, $"/greetings/{greeting.CreatedAt.Ticks}"), greeting);
});

app.MapGet("/greetings", async (IGreetingRepository repository, int? count) =>
{
    var greetings = await repository.GetRecentAsync(count ?? 10);
    return Results.Ok(greetings);
});

app.MapHealthChecks("/health");

await app.RunAsync();
