using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DisposableSingletonFactoryE128AnalyzerTests
{
    private static readonly ReferenceAssemblies Net80WithDi = ReferenceAssemblies.Net.Net80
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "8.0.0")]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<DisposableSingletonFactoryAnalyzer, DefaultVerifier>
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
    public Task AddSingleton_FactoryLambda_ReturnsDisposable_Fires()
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
                    services.AddSingleton({|E128031:sp => new MyService()|});
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_FactoryLambda_ParenthesizedLambda_ReturnsDisposable_Fires()
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
                    services.AddSingleton({|E128031:(IServiceProvider sp) => new MyService()|});
                }
            }
            """);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_GenericOverload_NoFire()
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
    public Task AddSingleton_FactoryLambda_NonDisposable_NoFire()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.DependencyInjection;
            class MyService { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddSingleton(sp => new MyService());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddScoped_FactoryLambda_Disposable_NoFire()
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
                    services.AddScoped(sp => new MyService());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddSingleton_GenericWithLambda_StillFires()
    {
        // Even with a generic type arg, the lambda still returns an IDisposable.
        return VerifyAsync("""
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
            """);
    }

    #endregion Does Not Fire
}
