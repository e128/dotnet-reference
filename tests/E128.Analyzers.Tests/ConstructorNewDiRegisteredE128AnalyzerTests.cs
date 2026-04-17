using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConstructorNewDiRegisteredE128AnalyzerTests
{
    private static readonly ReferenceAssemblies Net100WithDi = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.6")]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConstructorNewDiRegisteredAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithDi
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorNew_DiRegisteredType_Fires()
    {
        return VerifyAsync("""
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorNew_DiRegisteredViaAddScoped_Fires()
    {
        return VerifyAsync("""
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
                           """);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstructorNew_NotDiRegistered_NoFire()
    {
        return VerifyAsync("""
                           class MyService { }
                           class Consumer
                           {
                               public Consumer()
                               {
                                   var svc = new MyService();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MethodNew_DiRegistered_NoFire()
    {
        return VerifyAsync("""
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
                           """);
    }

    #endregion Does Not Fire
}
