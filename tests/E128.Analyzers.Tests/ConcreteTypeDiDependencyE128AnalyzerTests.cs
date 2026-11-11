using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcreteTypeDiDependencyE128AnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithDi };

    private static readonly ReferenceAssemblies Net100WithDi = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.6")]);

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task ConcreteParam_OnlyInterfaceRegistered_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteTypeDiDependencyAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           interface IMyService { }
                           class MyService : IMyService { }
                           class Consumer
                           {
                               public Consumer({|E128035:MyService svc|}) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<IMyService, MyService>();
                               }
                           }
                           """, options: Options);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task ConcreteParam_DirectlyRegistered_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteTypeDiDependencyAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           interface IMyService { }
                           class MyService : IMyService { }
                           class Consumer
                           {
                               public Consumer(MyService svc) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<IMyService, MyService>();
                                   services.AddSingleton<MyService>();
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InterfaceParam_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteTypeDiDependencyAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           interface IMyService { }
                           class MyService : IMyService { }
                           class Consumer
                           {
                               public Consumer(IMyService svc) { }
                           }
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
    public Task ConcreteParam_NotRegisteredAtAll_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteTypeDiDependencyAnalyzer>("""
                           class MyService { }
                           class Consumer
                           {
                               public Consumer(MyService svc) { }
                           }
                           """, options: Options);
    }

    #endregion Does Not Fire
}
