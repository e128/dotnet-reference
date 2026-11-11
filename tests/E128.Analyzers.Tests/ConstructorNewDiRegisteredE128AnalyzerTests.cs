using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConstructorNewDiRegisteredE128AnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithDi };

    private static readonly ReferenceAssemblies Net100WithDi = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.6")]);

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorNew_DiRegisteredType_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConstructorNewDiRegisteredAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           class MyService { }
                           class Consumer
                           {
                               public Consumer()
                               {
                                   var svc = {|E128034:new MyService()|};
                               }
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
    public Task ConstructorNew_DiRegisteredViaAddScoped_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConstructorNewDiRegisteredAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           class MyService { }
                           class Consumer
                           {
                               public Consumer()
                               {
                                   var svc = {|E128034:new MyService()|};
                               }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddScoped<MyService>();
                               }
                           }
                           """, options: Options);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorNew_NotDiRegistered_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ConstructorNewDiRegisteredAnalyzer>("""
                           class MyService { }
                           class Consumer
                           {
                               public Consumer()
                               {
                                   var svc = new MyService();
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MethodNew_DiRegistered_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ConstructorNewDiRegisteredAnalyzer>("""
                           using Microsoft.Extensions.DependencyInjection;
                           class MyService { }
                           class Consumer
                           {
                               void DoWork()
                               {
                                   var svc = new MyService();
                               }
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

    #endregion Does Not Fire
}
