using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcreteTypeDiDependencyCodeFixNoiseBranchCoverageTests
{
    private static readonly ReferenceAssemblies References = TestReferences.Net100WithDependencyInjection;

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RewritesTheMatchingRegistrationAmongOtherCalls()
    {
        return CodeFixTestRunner.RunAsync<ConcreteTypeDiDependencyAnalyzer, ConcreteTypeDiDependencyCodeFixProvider>(
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IService { }
            interface IOther { }
            class Service : IService { }
            class Other : IOther { }
            class Consumer
            {
                public Consumer({|E128035:Service service|}) { }
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    Helper();
                    services.Clear();
                    services.AddSingleton<Other>();
                    services.AddSingleton<IOther, Other>();
                    services.AddSingleton<IService, Service>();
                }

                static void Helper() { }
            }
            """,
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IService { }
            interface IOther { }
            class Service : IService { }
            class Other : IOther { }
            class Consumer
            {
                public Consumer(Service service) { }
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    Helper();
                    services.Clear();
                    services.AddSingleton<Other>();
                    services.AddSingleton<IOther, Other>();
                    services.AddSingleton<Service>();
                    services.AddSingleton<IService>(sp => sp.GetRequiredService<Service>());
                }

                static void Helper() { }
            }
            """,
            references: References,
            behaviors: CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            options: new CodeFixTestOptions { FixAllIterations = 1 });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheRegistrationIsPartOfAFluentChain()
    {
        return CodeFixTestRunner.NoFixAsync<ConcreteTypeDiDependencyAnalyzer, ConcreteTypeDiDependencyCodeFixProvider>(
            """
            using Microsoft.Extensions.DependencyInjection;
            class Other { }
            interface IService { }
            class Service : IService { }
            class Consumer
            {
                public Consumer({|E128035:Service service|}) { }
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddSingleton<IService, Service>().AddScoped<Other>();
                }
            }
            """,
            References,
            CodeFixTestBehaviors.SkipLocalDiagnosticCheck);
    }
}
