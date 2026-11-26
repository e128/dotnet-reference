using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CaptiveDependencyBranchCoverageGapTests
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
    public Task Silent_ForARegistrationCallThatDoesNotBind()
    {
        return VerifyAsync(ReferenceAssemblies.Net.Net100, """
            interface IServiceCollection { }
            class Repository { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.{|CS1061:AddScoped<Repository>|}();
                    services.{|CS1061:AddSingleton<Repository>|}();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForSingletonTypeArgumentsThatAreNotNamedTypes()
    {
        return VerifyAsync(TestReferences.Net100WithEfCore, """
            using Microsoft.Extensions.DependencyInjection;
            class Repository { }
            class Startup
            {
                void Configure<T>(IServiceCollection services) where T : class
                {
                    services.AddSingleton<Repository[]>();
                    services.AddSingleton<T>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAContextHelperWhoseTypeofArgumentIsNotAContext()
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
                    services.AddDbContext(typeof(string), ServiceLifetime.Singleton);
                    services.AddDbContext(typeof(StoreContext), ServiceLifetime.Scoped);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAContextHelperWhoseTypeArgumentsAreNotContexts()
    {
        return VerifyAsync(TestReferences.Net100WithEfCore, """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.Extensions.DependencyInjection;
            class StoreContext : DbContext { }
            static class Helpers
            {
                public static IServiceCollection AddDbContext<T>(
                    this IServiceCollection services,
                    ServiceLifetime contextLifetime) => services;
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddDbContext<string[]>(ServiceLifetime.Singleton);
                    services.AddDbContext<string>(ServiceLifetime.Singleton);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnTheServiceTypeWhenTheContextIsTheImplementation()
    {
        return VerifyAsync(TestReferences.Net100WithEfCore, """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.Extensions.DependencyInjection;
            interface IStoreContext { }
            class StoreContext : DbContext, IStoreContext { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddDbContext<{|E128154:IStoreContext|}, StoreContext>(options => { }, ServiceLifetime.Singleton);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnTheCallWhenTheContextTypeIsInferred()
    {
        return VerifyAsync(TestReferences.Net100WithEfCore, """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.Extensions.DependencyInjection;
            class StoreContext : DbContext { }
            static class Helpers
            {
                public static IServiceCollection AddDbContext<T>(
                    this IServiceCollection services,
                    T instance,
                    ServiceLifetime contextLifetime) => services;
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    {|E128154:services.AddDbContext(new StoreContext(), ServiceLifetime.Singleton)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheContextLifetimeIsAConvertedConstant()
    {
        return VerifyAsync(TestReferences.Net100WithEfCore, """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.Extensions.DependencyInjection;
            class StoreContext : DbContext { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddDbContext<{|E128154:StoreContext|}>(options => { }, (ServiceLifetime)0);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnTheCallWhenTheSingletonTypeComesFromAServiceKey()
    {
        return VerifyAsync(TestReferences.Net100WithDependencyInjection, """
            using Microsoft.Extensions.DependencyInjection;
            class Repository { }
            class Worker
            {
                public Worker() { }
                public Worker(Repository repository) { }
            }
            static class Helpers
            {
                public static IServiceCollection AddSingleton<T>(this IServiceCollection services, T serviceKey)
                    where T : class => services;
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddScoped<Repository>();
                    {|E128154:services.AddSingleton(new Worker())|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForSingletonHelpersWithoutOneOrTwoTypeArguments()
    {
        return VerifyAsync(TestReferences.Net100WithDependencyInjection, """
            using Microsoft.Extensions.DependencyInjection;
            class Repository { }
            class Worker { public Worker(Repository repository) { } }
            static class Helpers
            {
                public static IServiceCollection AddSingleton(this IServiceCollection services) => services;

                public static IServiceCollection AddSingleton<A, B, C>(this IServiceCollection services) => services;
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddScoped<Repository>();
                    services.AddSingleton();
                    services.AddSingleton<Worker, Worker, Worker>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAContextRegistrationWhenOnlyTheServiceCollectionTypeIsAvailable()
    {
        return VerifyAsync(ReferenceAssemblies.Net.Net100, """
            namespace Microsoft.EntityFrameworkCore
            {
                public class DbContext { }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                public enum ServiceLifetime { Singleton, Scoped, Transient }

                public interface IServiceCollection { }
            }

            namespace App
            {
                using Microsoft.EntityFrameworkCore;
                using Microsoft.Extensions.DependencyInjection;

                class StoreContext : DbContext { }

                static class Helpers
                {
                    public static IServiceCollection AddDbContext<T>(
                        this IServiceCollection services,
                        ServiceLifetime contextLifetime) where T : DbContext => services;
                }

                class Startup
                {
                    void Configure(IServiceCollection services)
                    {
                        services.AddDbContext<{|E128154:StoreContext|}>(ServiceLifetime.Singleton);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAContextRegistrationOnAReceiverThatIsNotAServiceCollection()
    {
        return VerifyAsync(ReferenceAssemblies.Net.Net100, """
            namespace Microsoft.EntityFrameworkCore
            {
                public class DbContext { }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                public enum ServiceLifetime { Singleton, Scoped, Transient }

                public interface IServiceCollection { }
            }

            namespace App
            {
                using Microsoft.EntityFrameworkCore;
                using Microsoft.Extensions.DependencyInjection;

                class StoreContext : DbContext { }

                class Registry
                {
                    public void AddDbContext<T>(ServiceLifetime contextLifetime) where T : DbContext { }
                }

                class Startup
                {
                    void Configure(Registry registry)
                    {
                        registry.AddDbContext<StoreContext>(ServiceLifetime.Singleton);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAContextRegistrationWhenNoServiceCollectionTypeExists()
    {
        return VerifyAsync(ReferenceAssemblies.Net.Net100, """
            namespace Microsoft.EntityFrameworkCore
            {
                public class DbContext { }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                public enum ServiceLifetime { Singleton, Scoped, Transient }
            }

            namespace App
            {
                using Microsoft.EntityFrameworkCore;
                using Microsoft.Extensions.DependencyInjection;

                class StoreContext : DbContext { }

                class Registry
                {
                    public void AddDbContext<T>(ServiceLifetime contextLifetime) where T : DbContext { }
                }

                class Startup
                {
                    void Configure(Registry registry)
                    {
                        registry.AddDbContext<StoreContext>(ServiceLifetime.Singleton);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheServiceLifetimeTypeHasNoSingletonMember()
    {
        return VerifyAsync(ReferenceAssemblies.Net.Net100, """
            namespace Microsoft.EntityFrameworkCore
            {
                public class DbContext { }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                public enum ServiceLifetime { Scoped, Transient }

                public interface IServiceCollection { }
            }

            namespace App
            {
                using Microsoft.EntityFrameworkCore;
                using Microsoft.Extensions.DependencyInjection;

                class StoreContext : DbContext { }

                static class Helpers
                {
                    public static IServiceCollection AddDbContext<T>(
                        this IServiceCollection services,
                        ServiceLifetime contextLifetime) where T : DbContext => services;
                }

                class Startup
                {
                    void Configure(IServiceCollection services)
                    {
                        services.AddDbContext<StoreContext>(ServiceLifetime.Scoped);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSingletonMemberOfTheServiceLifetimeTypeIsAProperty()
    {
        return VerifyAsync(ReferenceAssemblies.Net.Net100, """
            namespace Microsoft.EntityFrameworkCore
            {
                public class DbContext { }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                public sealed class ServiceLifetime
                {
                    public static ServiceLifetime Singleton => new ServiceLifetime();
                }

                public interface IServiceCollection { }
            }

            namespace App
            {
                using Microsoft.EntityFrameworkCore;
                using Microsoft.Extensions.DependencyInjection;

                class StoreContext : DbContext { }

                static class Helpers
                {
                    public static IServiceCollection AddDbContext<T>(
                        this IServiceCollection services,
                        ServiceLifetime contextLifetime) where T : DbContext => services;
                }

                class Startup
                {
                    void Configure(IServiceCollection services)
                    {
                        services.AddDbContext<StoreContext>(ServiceLifetime.Singleton);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSingletonFieldOfTheServiceLifetimeTypeHasNoConstant()
    {
        return VerifyAsync(ReferenceAssemblies.Net.Net100, """
            namespace Microsoft.EntityFrameworkCore
            {
                public class DbContext { }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                public sealed class ServiceLifetime
                {
                    public static readonly ServiceLifetime Singleton = new ServiceLifetime();
                }

                public interface IServiceCollection { }
            }

            namespace App
            {
                using Microsoft.EntityFrameworkCore;
                using Microsoft.Extensions.DependencyInjection;

                class StoreContext : DbContext { }

                static class Helpers
                {
                    public static IServiceCollection AddDbContext<T>(
                        this IServiceCollection services,
                        ServiceLifetime contextLifetime) where T : DbContext => services;
                }

                class Startup
                {
                    void Configure(IServiceCollection services)
                    {
                        services.AddDbContext<StoreContext>(ServiceLifetime.Singleton);
                    }
                }
            }
            """);
    }
}
