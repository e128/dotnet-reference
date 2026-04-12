using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcreteTypeDiDependencyE128AnalyzerTests
{
    private static readonly ReferenceAssemblies Net80WithDi = ReferenceAssemblies.Net.Net80
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "8.0.0")]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConcreteTypeDiDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net80WithDi,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task ConcreteParam_OnlyInterfaceRegistered_Fires()
    {
        return VerifyAsync("""
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
            """);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task ConcreteParam_DirectlyRegistered_NoFire()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InterfaceParam_NoFire()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConcreteParam_NotRegisteredAtAll_NoFire()
    {
        return VerifyAsync("""
            class MyService { }
            class Consumer
            {
                public Consumer(MyService svc) { }
            }
            """);
    }

    #endregion Does Not Fire
}
