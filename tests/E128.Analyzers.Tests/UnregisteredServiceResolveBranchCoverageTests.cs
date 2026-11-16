using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the registration and resolve shapes of the DI rule with local stand-ins for the
///     dependency injection API, so the tests need no package download.
/// </summary>
public sealed class UnregisteredServiceResolveBranchCoverageTests
{
    private const string Stubs = """
                                 using System;
                                 using System.Net.Http;
                                 using Microsoft.Extensions.DependencyInjection;

                                 namespace System.Net.Http
                                 {
                                     public interface IHttpClientFactory
                                     {
                                         HttpClient CreateClient(string name);
                                     }
                                 }

                                 namespace Microsoft.Extensions.DependencyInjection
                                 {
                                     public interface IServiceCollection
                                     {
                                     }

                                     public static class ServiceProviderServiceExtensions
                                     {
                                         public static T GetRequiredService<T>(this IServiceProvider provider) where T : notnull => default!;

                                         public static object GetRequiredService(this IServiceProvider provider, Type serviceType) => default!;
                                     }

                                     public static class ServiceCollectionServiceExtensions
                                     {
                                         public static IServiceCollection AddSingleton<T>(this IServiceCollection services) where T : class => services;

                                         public static IServiceCollection AddSingleton(this IServiceCollection services, Type service, Type implementation) => services;
                                     }

                                     public static class HttpClientFactoryServiceCollectionExtensions
                                     {
                                         public static IServiceCollection AddHttpClient(this IServiceCollection services, string name) => services;

                                         public static IServiceCollection AddHttpClient<T>(this IServiceCollection services) where T : class => services;
                                     }
                                 }

                                 class Service
                                 {
                                 }

                                 """;

    private static readonly AnalyzerTestOptions Options = new() { MarkupOptions = MarkupOptions.UseFirstDescriptor };

    private static Task VerifyAsync(string source, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<UnregisteredServiceResolveAnalyzer>(
            Stubs + source,
            expected: null,
            options ?? Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheResolveSitsInALambdaThatIsNotAnArgument()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M()
                               {
                                   Func<IServiceProvider, Service> create = sp => {|E128103:sp.GetRequiredService<Service>()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheResolveSitsInALambdaPassedToANonRegistrationCall()
    {
        return VerifyAsync("""
                           class C
                           {
                               static void Run(Func<IServiceProvider, Service> create)
                               {
                               }

                               void M()
                               {
                                   Run(sp => {|E128103:sp.GetRequiredService<Service>()|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheResolveSitsInALambdaPassedToAnIndexedCallback()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(Action<Func<IServiceProvider, Service>>[] callbacks)
                               {
                                   callbacks[0](sp => {|E128103:sp.GetRequiredService<Service>()|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheResolveSitsInAnAddFactoryLambda()
    {
        return VerifyAsync("""
                           static class Extensions
                           {
                               public static IServiceCollection AddThing(this IServiceCollection services, Func<IServiceProvider, Service> factory) => services;
                           }

                           class C
                           {
                               void M(IServiceCollection services)
                               {
                                   services.AddThing(sp => sp.GetRequiredService<Service>());
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheResolveUsesATypeArgumentThatIsNotNamed()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M<T>(IServiceProvider provider)
                                   where T : notnull
                               {
                                   provider.GetRequiredService<T>();
                                   provider.GetRequiredService<int[]>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheResolveUsesTheNonGenericOverload()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(IServiceProvider provider)
                               {
                                   provider.GetRequiredService(typeof(Service));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInvokedExpressionHasNoName()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(Action[] actions)
                               {
                                   actions[0]();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnAddCallDoesNotBind()
    {
        return VerifyAsync(
            """
            class C
            {
                void M(IServiceCollection services)
                {
                    services.AddMissing<Service>();
                    services.GetRequiredService<Service>();
                }
            }
            """,
            new AnalyzerTestOptions
            {
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                CompilerDiagnostics = CompilerDiagnostics.None
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRegistrationUsesTypeArgumentsThatAreNotNamed()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M<T>(IServiceCollection services)
                                   where T : class
                               {
                                   services.AddSingleton<T>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnlyParametersThatCanBeServices()
    {
        return VerifyAsync("""
                           struct Settings
                           {
                           }

                           class Missing
                           {
                           }

                           class Consumer
                           {
                               public Consumer(int count, string name, Settings settings, Service[] items, {|E128103:Missing missing|})
                               {
                               }
                           }

                           class C
                           {
                               void M(IServiceCollection services)
                               {
                                   services.AddSingleton<Consumer>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheNamedClientCallDoesNotBind()
    {
        return VerifyAsync(
            """
            class C
            {
                void M(IServiceCollection services, IHttpClientFactory factory)
                {
                    services.AddHttpClient(missing);
                    factory.CreateClient(missing);
                    unknown.CreateClient("x");
                }
            }
            """,
            new AnalyzerTestOptions
            {
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                CompilerDiagnostics = CompilerDiagnostics.None
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheClientNameIsNotAConstant()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(IHttpClientFactory factory, string name)
                               {
                                   factory.CreateClient(name);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRegisteredClientNameIsEmpty()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(IServiceCollection services, IHttpClientFactory factory)
                               {
                                   services.AddHttpClient("");
                                   factory.CreateClient("");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypedClientRegistrationCarriesNoName()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(IServiceCollection services)
                               {
                                   services.AddHttpClient<Service>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheOpenGenericRegistrationNamesAnotherType()
    {
        return VerifyAsync("""
                           interface IRepo<T>
                           {
                           }

                           class Repo<T> : IRepo<T>
                           {
                           }

                           class C
                           {
                               void M(IServiceCollection services, IServiceProvider provider)
                               {
                                   services.AddSingleton(typeof(IRepo<>), typeof(Repo<>));
                                   services.AddSingleton(Type.GetType("x")!, Type.GetType("y")!);
                                   provider.GetRequiredService<IRepo<int>>();
                                   {|E128103:provider.GetRequiredService<Service>()|};
                               }
                           }
                           """);
    }
}
