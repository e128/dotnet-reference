using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     E128035 uses CompilationEnd (non-local diagnostics) and the code fix modifies a different
///     location than the diagnostic. The standard code fix test framework does not support non-local
///     fixes. These tests verify the analyzer fires correctly via the analyzer test harness.
/// </summary>
public sealed class ConcreteTypeDiDependencyE128CodeFixTests
{

    private static AnalyzerTestOptions Options => new() { References = Net100WithDi };

    private static readonly ReferenceAssemblies Net100WithDi = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.6")]);

    [Fact]
    [Trait("Category", "CI")]
    public Task AnalyzerFires_WhenConcreteParamOnlyInterfaceRegistered()
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

    [Fact]
    [Trait("Category", "CI")]
    public Task AnalyzerDoesNotFire_WhenDirectRegistrationExists()
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
}
