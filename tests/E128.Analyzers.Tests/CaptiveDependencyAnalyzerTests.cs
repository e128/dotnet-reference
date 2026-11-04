using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CaptiveDependencyAnalyzerTests
{
    private static readonly ReferenceAssemblies Net100WithDi = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.6"),
            new PackageIdentity("Microsoft.Extensions.Hosting.Abstractions", "10.0.6")
        ]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<CaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithDi
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Singleton_DependsOnScoped_Fires()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.DependencyInjection;
                           class Repository { }
                           class Worker
                           {
                               public Worker(Repository repository) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddScoped<Repository>();
                                   services.AddSingleton<{|E128154:Worker|}>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TryAddSingleton_DependsOnScoped_Fires()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.DependencyInjection;
                           using Microsoft.Extensions.DependencyInjection.Extensions;
                           class Repository { }
                           class Worker
                           {
                               public Worker(Repository repository) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.TryAddScoped<Repository>();
                                   services.TryAddSingleton<{|E128154:Worker|}>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddKeyedSingleton_DependsOnKeyedScoped_Fires()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.DependencyInjection;
                           class Repository { }
                           interface IWorker { }
                           class Worker : IWorker
                           {
                               public Worker(Repository repository) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddKeyedScoped<Repository>("repo");
                                   services.AddKeyedSingleton<IWorker, {|E128154:Worker|}>("worker");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TypeOfOverloads_DependsOnScoped_Fires()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.DependencyInjection;
                           class Repository { }
                           interface IWorker { }
                           class Worker : IWorker
                           {
                               public Worker(Repository repository) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddScoped(typeof(Repository));
                                   services.AddSingleton(typeof(IWorker), {|E128154:typeof(Worker)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddHostedService_DependsOnScoped_Fires()
    {
        return VerifyAsync("""
                           using System.Threading;
                           using System.Threading.Tasks;
                           using Microsoft.Extensions.DependencyInjection;
                           using Microsoft.Extensions.Hosting;
                           class Repository { }
                           class Worker : BackgroundService
                           {
                               public Worker(Repository repository) { }

                               protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddScoped<Repository>();
                                   services.AddHostedService<{|E128154:Worker|}>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Singleton_InterfaceRegistration_DependsOnScoped_Fires()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.DependencyInjection;
                           class Repository { }
                           interface IWorker { }
                           class Worker : IWorker
                           {
                               public Worker(Repository repository) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddScoped<Repository>();
                                   services.AddSingleton<IWorker, {|E128154:Worker|}>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Singleton_DependsOnSingleton_DoesNotFire()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.DependencyInjection;
                           class Cache { }
                           class Worker
                           {
                               public Worker(Cache cache) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<Cache>();
                                   services.AddSingleton<Worker>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Singleton_TakesScopeFactory_DoesNotFire()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.DependencyInjection;
                           class Worker
                           {
                               public Worker(IServiceScopeFactory scopeFactory) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddScoped<IServiceScopeFactory>();
                                   services.AddSingleton<Worker>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Singleton_FactoryRegistration_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using Microsoft.Extensions.DependencyInjection;
                           class Repository { }
                           class Worker
                           {
                               public Worker(Repository repository) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddScoped<Repository>();
                                   services.AddSingleton<Worker>(provider => new Worker(provider.GetRequiredService<Repository>()));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Singleton_InstanceRegistration_DoesNotFire()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.DependencyInjection;
                           class Repository { }
                           class Worker
                           {
                               public Worker(Repository repository) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddScoped<Repository>();
                                   services.AddSingleton(new Worker(new Repository()));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ScopedOnly_DoesNotFire()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.DependencyInjection;
                           class Repository { }
                           class Worker
                           {
                               public Worker(Repository repository) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddScoped<Repository>();
                                   services.AddScoped<Worker>();
                               }
                           }
                           """);
    }
}
