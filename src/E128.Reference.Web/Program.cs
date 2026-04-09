using E128.Reference.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Greeter>();

var app = builder.Build();

app.MapGet("/", (Greeter greeter) => greeter.Greet());
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

await app.RunAsync();
