using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CaptiveDependencyBranchCoverageTests
{
    private static Task VerifyAsync(ReferenceAssemblies references, string code)
    {
        // Two descriptors share the E128154 id, so markup names the first one.
        return AnalyzerTestRunner.VerifyAsync<CaptiveDependencyAnalyzer>(
            code,
            expected: null,
            options: new AnalyzerTestOptions { References = references, MarkupOptions = MarkupOptions.UseFirstDescriptor });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NextToAnUnqualifiedCall()
    {
        return VerifyAsync(TestReferences.Net100WithDependencyInjection, """
            using Microsoft.Extensions.DependencyInjection;
            class Repository { }
            class Worker { public Worker(Repository repository) { } }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddScoped<Repository>();
                    services.AddSingleton<{|E128154:Worker|}>();
                    Helper();
                }

                static void Helper() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForSingletonsThatNameNoConcreteImplementation()
    {
        return VerifyAsync(TestReferences.Net100WithEfCore, """
            using Microsoft.Extensions.DependencyInjection;
            interface IWorker { }
            abstract class AbstractWorker { }
            class Repository { }
            class Worker : IWorker { public Worker(Repository repository) { } }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddScoped<Repository>();
                    services.AddSingleton<IWorker>();
                    services.AddSingleton<AbstractWorker>();
                    services.AddSingleton(typeof(IWorker));
                    services.AddSingleton(typeof(AbstractWorker));
                    services.AddSingleton(typeof(IWorker), typeof(Worker[]));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForScopedRegistrationsOfTypeParametersAndArrays()
    {
        return VerifyAsync(TestReferences.Net100WithDependencyInjection, """
            using Microsoft.Extensions.DependencyInjection;
            class Repository { }
            class Worker { public Worker(Repository repository) { } }
            class Startup
            {
                void Configure<T>(IServiceCollection services) where T : class
                {
                    services.AddScoped<T>();
                    services.AddScoped<Repository[]>();
                    services.AddSingleton<Worker>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WithoutAnyDependencyInjectionPackage()
    {
        return VerifyAsync(ReferenceAssemblies.Net.Net100, """
            interface IServiceCollection { }
            static class Registrations
            {
                public static IServiceCollection AddScoped<T>(this IServiceCollection services) where T : class => services;

                public static IServiceCollection AddSingleton<T>(this IServiceCollection services) where T : class => services;
            }
            class Repository { }
            class Worker { public Worker(Repository repository) { } }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddScoped<Repository>();
                    services.AddSingleton<{|E128154:Worker|}>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForATypeofContextOnAUserRegistrationHelper()
    {
        return VerifyAsync(TestReferences.Net100WithEfCore, """
            using System;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.Extensions.DependencyInjection;
            class StoreContext : DbContext { }
            static class Helpers
            {
                public static IServiceCollection AddDbContext(
                    this IServiceCollection services,
                    Type contextType,
                    ServiceLifetime contextLifetime) => services;
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddDbContext({|E128154:typeof(StoreContext)|}, ServiceLifetime.Singleton);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAContextLifetimeThatIsNotAConstant()
    {
        return VerifyAsync(TestReferences.Net100WithEfCore, """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.Extensions.DependencyInjection;
            class StoreContext : DbContext { }
            class Startup
            {
                void Configure(IServiceCollection services, ServiceLifetime lifetime)
                {
                    services.AddDbContext<StoreContext>((DbContextOptionsBuilder builder) => { }, lifetime);
                }
            }
            """);
    }
}
