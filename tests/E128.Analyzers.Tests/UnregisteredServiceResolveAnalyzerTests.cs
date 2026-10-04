using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnregisteredServiceResolveAnalyzerTests
{
    private static readonly ReferenceAssemblies Net100WithDi = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.6")]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<UnregisteredServiceResolveAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithDi
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private static Task VerifyWithRegisteredServicesAsync(
        string registeredServices,
        string code,
        params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<UnregisteredServiceResolveAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net100WithDi
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", "is_global = true\ne128_registered_services = " + registeredServices));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task UnregisteredServiceResolveAnalyzer_ReportsResolve_WhenNoRegistrationExists()
    {
        return VerifyAsync("""
                           using System;
                           using Microsoft.Extensions.DependencyInjection;
                           interface IShape { }
                           class Square : IShape { }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<Square>();
                               }
                           }
                           class Consumer
                           {
                               void Resolve(IServiceProvider provider)
                               {
                                   var shape = {|E128103:provider.GetRequiredService<IShape>()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnregisteredServiceResolveAnalyzer_ReportsConstructorParameter_WhenRegisteredTypeDependsOnUnregisteredService()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.DependencyInjection;
                           interface IShape { }
                           class Consumer
                           {
                               public Consumer({|E128103:IShape shape|}) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<Consumer>();
                               }
                           }
                           """);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task UnregisteredServiceResolveAnalyzer_ReportsNothing_WhenGetServiceIsUsed()
    {
        return VerifyAsync("""
                           using System;
                           using Microsoft.Extensions.DependencyInjection;
                           interface IShape { }
                           class Consumer
                           {
                               void Resolve(IServiceProvider provider)
                               {
                                   var shape = provider.GetService<IShape>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnregisteredServiceResolveAnalyzer_ReportsNothing_WhenServiceIsFrameworkProvided()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using Microsoft.Extensions.DependencyInjection;
                           interface ILogger<T> { }
                           interface IOptions<T> { }
                           interface IOptionsSnapshot<T> { }
                           interface IOptionsMonitor<T> { }
                           interface IHttpClientFactory { }
                           interface IConfiguration { }
                           interface IHostEnvironment { }
                           class Consumer
                           {
                               void Resolve(IServiceProvider provider)
                               {
                                   var logger = provider.GetRequiredService<ILogger<Consumer>>();
                                   var options = provider.GetRequiredService<IOptions<Consumer>>();
                                   var snapshot = provider.GetRequiredService<IOptionsSnapshot<Consumer>>();
                                   var monitor = provider.GetRequiredService<IOptionsMonitor<Consumer>>();
                                   var factory = provider.GetRequiredService<IHttpClientFactory>();
                                   var configuration = provider.GetRequiredService<IConfiguration>();
                                   var environment = provider.GetRequiredService<IHostEnvironment>();
                                   var services = provider.GetRequiredService<IServiceProvider>();
                                   var consumers = provider.GetRequiredService<IEnumerable<Consumer>>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnregisteredServiceResolveAnalyzer_ReportsNothing_WhenServiceIsRegisteredAsInterfaceImplementation()
    {
        return VerifyAsync("""
                           using System;
                           using Microsoft.Extensions.DependencyInjection;
                           interface IShape { }
                           class Square : IShape { }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<IShape, Square>();
                               }
                           }
                           class Consumer
                           {
                               void Resolve(IServiceProvider provider)
                               {
                                   var shape = provider.GetRequiredService<IShape>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnregisteredServiceResolveAnalyzer_ReportsNothing_WhenServiceIsRegisteredAsOpenGeneric()
    {
        return VerifyAsync("""
                           using System;
                           using Microsoft.Extensions.DependencyInjection;
                           interface IRepository<T> { }
                           class Repository<T> : IRepository<T> { }
                           class Thing { }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton(typeof(IRepository<>), typeof(Repository<>));
                               }
                           }
                           class Consumer
                           {
                               void Resolve(IServiceProvider provider)
                               {
                                   var repository = provider.GetRequiredService<IRepository<Thing>>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnregisteredServiceResolveAnalyzer_ReportsNothing_WhenConstructorParameterIsOnUnregisteredType()
    {
        return VerifyAsync("""
                           using Microsoft.Extensions.DependencyInjection;
                           interface IShape { }
                           class Square : IShape { }
                           class Consumer
                           {
                               public Consumer(IShape shape) { }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<Square>();
                               }
                           }
                           """);
    }

    #endregion Does Not Fire

    #region Acceptance Contract

    [Fact]
    [Trait("Category", "CI")]
    public Task UnregisteredServiceResolveAnalyzer_ReportsResolve_WhenServiceIsUnregisteredAndAbsentFromOption()
    {
        return VerifyWithRegisteredServicesAsync(
            "IWidget",
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            interface IShape { }
            class Consumer
            {
                void Resolve(IServiceProvider provider)
                {
                    var shape = {|E128103:provider.GetRequiredService<IShape>()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnregisteredServiceResolveAnalyzer_ReportsNothing_WhenServiceIsListedInTheOption()
    {
        return VerifyWithRegisteredServicesAsync(
            "IShape",
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            interface IShape { }
            class Consumer
            {
                void Resolve(IServiceProvider provider)
                {
                    var shape = provider.GetRequiredService<IShape>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnregisteredServiceResolveAnalyzer_ReportsNothing_WhenServiceIsFrameworkProvidedWithoutAGenericRegistration()
    {
        return VerifyAsync("""
                           using System;
                           using Microsoft.Extensions.DependencyInjection;
                           namespace Stubs
                           {
                               interface ILoggerFactory { }
                               interface HttpClient { }
                               interface HybridCache { }
                               interface IServer { }
                               interface TracerProvider { }
                               interface IChatCompletionService { }
                               interface ITextEmbeddingService { }
                               interface IImageEmbeddingService { }
                           }
                           class Consumer
                           {
                               void Resolve(IServiceProvider provider)
                               {
                                   var loggerFactory = provider.GetRequiredService<Stubs.ILoggerFactory>();
                                   var client = provider.GetRequiredService<Stubs.HttpClient>();
                                   var cache = provider.GetRequiredService<Stubs.HybridCache>();
                                   var server = provider.GetRequiredService<Stubs.IServer>();
                                   var tracer = provider.GetRequiredService<Stubs.TracerProvider>();
                                   var chat = provider.GetRequiredService<Stubs.IChatCompletionService>();
                                   var text = provider.GetRequiredService<Stubs.ITextEmbeddingService>();
                                   var image = provider.GetRequiredService<Stubs.IImageEmbeddingService>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnregisteredServiceResolveAnalyzer_ReportsNothing_WhenResolveSitsInsideRegistrationFactory()
    {
        return VerifyAsync("""
                           using System;
                           using Microsoft.Extensions.DependencyInjection;
                           interface IShape { }
                           interface IUnregistered { }
                           class Consumer
                           {
                               void Resolve(IServiceProvider provider)
                               {
                                   var shape = {|E128103:provider.GetRequiredService<IUnregistered>()|};
                               }
                           }
                           class Startup
                           {
                               void Configure(IServiceCollection services)
                               {
                                   services.AddSingleton<IShape>(sp =>
                                   {
                                       var unused = sp.GetRequiredService<IUnregistered>();
                                       return new Shape();
                                   });
                               }
                           }
                           class Shape : IShape { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnregisteredServiceResolveAnalyzer_ReportsNothing_WhenOptionEntriesCarryPadding()
    {
        return VerifyWithRegisteredServicesAsync(
            "  IShape ,  IWidget  ",
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            interface IShape { }
            interface IWidget { }
            class Consumer
            {
                void Resolve(IServiceProvider provider)
                {
                    var shape = provider.GetRequiredService<IShape>();
                    var widget = provider.GetRequiredService<IWidget>();
                }
            }
            """);
    }

    #endregion Acceptance Contract
}
