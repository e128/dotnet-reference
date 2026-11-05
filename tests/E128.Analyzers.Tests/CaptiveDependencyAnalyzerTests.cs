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

    private static readonly ReferenceAssemblies Net100WithEfCore = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.14"),
            new PackageIdentity("Microsoft.EntityFrameworkCore", "10.0.14")
        ]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return VerifyAsync(Net100WithDi, code, expected);
    }

    private static Task VerifyAsync(ReferenceAssemblies references, string code, params DiagnosticResult[] expected)
    {
        // Two descriptors share the E128154 id, so markup needs a rule for which one it names.
        var test = new CSharpAnalyzerTest<CaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = references,
            MarkupOptions = MarkupOptions.UseFirstDescriptor
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private static Task VerifyEfCoreAsync(string code, params DiagnosticResult[] expected)
    {
        return VerifyAsync(Net100WithEfCore, code, expected);
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

    [Fact]
    [Trait("Category", "CI")]
    public Task Singleton_DbContext_Fires()
    {
        return VerifyEfCoreAsync("""
                                 using Microsoft.EntityFrameworkCore;
                                 using Microsoft.Extensions.DependencyInjection;
                                 class StoreContext : DbContext { }
                                 class Startup
                                 {
                                     void Configure(IServiceCollection services)
                                     {
                                         services.AddSingleton<{|E128154:StoreContext|}>();
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Singleton_InterfaceToDbContext_Fires()
    {
        return VerifyEfCoreAsync("""
                                 using Microsoft.EntityFrameworkCore;
                                 using Microsoft.Extensions.DependencyInjection;
                                 interface IStore { }
                                 class StoreContext : DbContext, IStore { }
                                 class Startup
                                 {
                                     void Configure(IServiceCollection services)
                                     {
                                         services.AddSingleton<IStore, {|E128154:StoreContext|}>();
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Singleton_DbContextFactory_Fires()
    {
        return VerifyEfCoreAsync("""
                                 using Microsoft.EntityFrameworkCore;
                                 using Microsoft.Extensions.DependencyInjection;
                                 class StoreContext : DbContext { }
                                 class Startup
                                 {
                                     void Configure(IServiceCollection services)
                                     {
                                         services.AddSingleton<{|E128154:StoreContext|}>(provider => new StoreContext());
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Singleton_TypeofDbContext_Fires()
    {
        return VerifyEfCoreAsync("""
                                 using Microsoft.EntityFrameworkCore;
                                 using Microsoft.Extensions.DependencyInjection;
                                 class StoreContext : DbContext { }
                                 class Startup
                                 {
                                     void Configure(IServiceCollection services)
                                     {
                                         services.AddSingleton({|E128154:typeof(StoreContext)|});
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddDbContext_SingletonLifetime_Fires()
    {
        return VerifyEfCoreAsync("""
                                 using Microsoft.EntityFrameworkCore;
                                 using Microsoft.Extensions.DependencyInjection;
                                 class StoreContext : DbContext { }
                                 class Startup
                                 {
                                     void Configure(IServiceCollection services)
                                     {
                                         services.AddDbContext<{|E128154:StoreContext|}>(options => { }, ServiceLifetime.Singleton);
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddDbContext_SingletonOptionsLifetimeOnly_DoesNotFire()
    {
        return VerifyEfCoreAsync("""
                                 using Microsoft.EntityFrameworkCore;
                                 using Microsoft.Extensions.DependencyInjection;
                                 class StoreContext : DbContext { }
                                 class Startup
                                 {
                                     void Configure(IServiceCollection services)
                                     {
                                         services.AddDbContext<StoreContext>(options => { }, optionsLifetime: ServiceLifetime.Singleton);
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddDbContext_NamedSingletonContextLifetime_Fires()
    {
        return VerifyEfCoreAsync("""
                                 using Microsoft.EntityFrameworkCore;
                                 using Microsoft.Extensions.DependencyInjection;
                                 class StoreContext : DbContext { }
                                 class Startup
                                 {
                                     void Configure(IServiceCollection services)
                                     {
                                         services.AddDbContext<{|E128154:StoreContext|}>(options => { }, contextLifetime: ServiceLifetime.Singleton);
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddDbContext_DefaultLifetime_DoesNotFire()
    {
        return VerifyEfCoreAsync("""
                                 using Microsoft.EntityFrameworkCore;
                                 using Microsoft.Extensions.DependencyInjection;
                                 class StoreContext : DbContext { }
                                 class Startup
                                 {
                                     void Configure(IServiceCollection services)
                                     {
                                         services.AddDbContext<StoreContext>();
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddScopedDbContext_DoesNotFire()
    {
        return VerifyEfCoreAsync("""
                                 using Microsoft.EntityFrameworkCore;
                                 using Microsoft.Extensions.DependencyInjection;
                                 class StoreContext : DbContext { }
                                 class Startup
                                 {
                                     void Configure(IServiceCollection services)
                                     {
                                         services.AddScoped<StoreContext>();
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddTransientDbContext_DoesNotFire()
    {
        return VerifyEfCoreAsync("""
                                 using Microsoft.EntityFrameworkCore;
                                 using Microsoft.Extensions.DependencyInjection;
                                 class StoreContext : DbContext { }
                                 class Startup
                                 {
                                     void Configure(IServiceCollection services)
                                     {
                                         services.AddTransient<StoreContext>();
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UserTypeNamedDbContext_DoesNotFire()
    {
        return VerifyEfCoreAsync("""
                                 using Microsoft.EntityFrameworkCore;
                                 using Microsoft.Extensions.DependencyInjection;
                                 namespace App
                                 {
                                     class DbContext { }
                                     class Startup
                                     {
                                         void Configure(IServiceCollection services)
                                         {
                                             services.AddSingleton<DbContext>();
                                         }
                                     }
                                 }
                                 """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoEfCoreReference_DoesNotFire()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.DependencyInjection;
                           class DbContext { }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<DbContext>();
                               }
                           }
                           """);
    }
}
