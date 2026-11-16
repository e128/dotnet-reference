using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConstructorNewDiRegisteredBranchCoverageTests
{
    private const string Stubs = """
                                 interface IServices { }
                                 static class Registrations
                                 {
                                     public static IServices AddSingleton<T>(this IServices services) where T : class => services;

                                     public static IServices AddScoped<T>(this IServices services) where T : class => services;

                                     public static IServices AddHttpClient(this IServices services, string name) => services;
                                 }
                                 class Service { }
                                 """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<ConstructorNewDiRegisteredAnalyzer>(Stubs + "\n" + code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnUnrelatedUnqualifiedCallAndANonGenericRegistrationAppearInTheCompilation()
    {
        return VerifyAsync("""
            class Consumer
            {
                public Consumer()
                {
                    var service = {|E128034:new Service()|};
                }
            }
            class Startup
            {
                void Configure(IServices services)
                {
                    Helper();
                    services.AddHttpClient("named");
                    services.AddSingleton<Service>();
                }

                static void Helper() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRegisteredTypeIsATypeParameter()
    {
        return VerifyAsync("""
            class Consumer
            {
                public Consumer()
                {
                    var service = new Service();
                }
            }
            class Startup
            {
                void Configure<T>(IServices services) where T : class
                {
                    services.AddSingleton<T>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCreationSitsInAMethodOrALocalFunctionInsideAConstructor()
    {
        return VerifyAsync("""
            class Consumer
            {
                public Consumer()
                {
                    Service Local() => new Service();
                    Local();
                }

                Service Create() => new Service();
            }
            class Startup
            {
                void Configure(IServices services)
                {
                    services.AddScoped<Service>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNoTypeIsRegistered()
    {
        return VerifyAsync("""
            class Consumer
            {
                public Consumer()
                {
                    var service = new Service();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForACreationOfAnUnregisteredType()
    {
        return VerifyAsync("""
            class Other { }
            class Consumer
            {
                public Consumer()
                {
                    var other = new Other();
                }
            }
            class Startup
            {
                void Configure(IServices services)
                {
                    services.AddSingleton<Service>();
                }
            }
            """);
    }
}
