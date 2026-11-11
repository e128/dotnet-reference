using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DisposableSingletonFactoryE128AnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithDi };

    private static readonly ReferenceAssemblies Net100WithDi = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.6")]);

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_FactoryLambda_ReturnsDisposable_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableSingletonFactoryAnalyzer>("""
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
                                   services.AddSingleton({|E128031:sp => new MyService()|});
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_FactoryLambda_ParenthesizedLambda_ReturnsDisposable_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableSingletonFactoryAnalyzer>("""
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
                                   services.AddSingleton({|E128031:(IServiceProvider sp) => new MyService()|});
                               }
                           }
                           """, options: Options);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_GenericOverload_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableSingletonFactoryAnalyzer>("""
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
    public Task AddSingleton_FactoryLambda_NonDisposable_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableSingletonFactoryAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           class MyService { }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton(sp => new MyService());
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddScoped_FactoryLambda_Disposable_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableSingletonFactoryAnalyzer>("""
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
                                   services.AddScoped(sp => new MyService());
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_GenericWithLambda_StillFires()
    {
        // Even with a generic type arg, the lambda still returns an IDisposable.
        return AnalyzerTestRunner.VerifyAsync<DisposableSingletonFactoryAnalyzer>("""
                           using System;
                           using Microsoft.Extensions.DependencyInjection;
                           interface IMyService { }
                           class MyService : IMyService, IDisposable
                           {
                               public void Dispose() { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<IMyService>({|E128031:sp => new MyService()|});
                               }
                           }
                           """, options: Options);
    }

    #endregion Does Not Fire
}
