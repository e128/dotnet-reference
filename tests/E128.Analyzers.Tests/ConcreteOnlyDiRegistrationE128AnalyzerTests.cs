using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcreteOnlyDiRegistrationE128AnalyzerTests
{
    private static readonly ReferenceAssemblies Net80WithDi = ReferenceAssemblies.Net.Net80
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "8.0.0")]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConcreteOnlyDiRegistrationAnalyzer, DefaultVerifier>
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
    public Task AddSingleton_ConcreteWithInterface_Fires()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddScoped_ConcreteWithInterface_Fires()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddTransient_ConcreteWithInterface_Fires()
    {
        return VerifyAsync("""
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
            """);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_NoInterface_NoFire()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.DependencyInjection;
            class MyService { }
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
    public Task AddSingleton_OnlyDisposable_NoFire()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_TwoTypeArgs_NoFire()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_InterfaceTypeArg_NoFire()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.DependencyInjection;
            interface IMyService { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddSingleton<IMyService>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_WithForwardingRegistration_NoFire()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_WithHttpClient_NoFire()
    {
        return VerifyAsync("""
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
            """);
    }

    #endregion Does Not Fire
}
