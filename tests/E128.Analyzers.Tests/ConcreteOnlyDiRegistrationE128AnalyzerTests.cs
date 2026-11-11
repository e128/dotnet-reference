using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcreteOnlyDiRegistrationE128AnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithDi };

    private static readonly ReferenceAssemblies Net100WithDi = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.6")]);

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_ConcreteWithInterface_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteOnlyDiRegistrationAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           interface IMyService { }
                           class MyService : IMyService { }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.{|E128032:AddSingleton<MyService>()|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddScoped_ConcreteWithInterface_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteOnlyDiRegistrationAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           interface IMyService { }
                           class MyService : IMyService { }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.{|E128032:AddScoped<MyService>()|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddTransient_ConcreteWithInterface_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteOnlyDiRegistrationAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           interface IMyService { }
                           class MyService : IMyService { }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.{|E128032:AddTransient<MyService>()|};
                               }
                           }
                           """, options: Options);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_NoInterface_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteOnlyDiRegistrationAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           class MyService { }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<MyService>();
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_OnlyDisposable_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteOnlyDiRegistrationAnalyzer>("""
                           using System;
                           using Microsoft.Extensions.DependencyInjection;
                           class MyService : IDisposable
                           {
                               public void Dispose() { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<MyService>();
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_TwoTypeArgs_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteOnlyDiRegistrationAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           interface IMyService { }
                           class MyService : IMyService { }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<IMyService, MyService>();
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_InterfaceTypeArg_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteOnlyDiRegistrationAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           interface IMyService { }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<IMyService>();
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_WithForwardingRegistration_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteOnlyDiRegistrationAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           interface IMyService { }
                           class MyService : IMyService { }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<MyService>();
                                   services.AddSingleton<IMyService>(sp => sp.GetRequiredService<MyService>());
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_WithHttpClient_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteOnlyDiRegistrationAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           interface IMyService { }
                           class MyService : IMyService { }
                           static class HttpClientExtensions
                           {
                               public static IServiceCollection AddHttpClient<T>(this IServiceCollection services) => services;
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddHttpClient<MyService>();
                                   services.AddSingleton<MyService>();
                               }
                           }
                           """, options: Options);
    }

    #endregion Does Not Fire
}
