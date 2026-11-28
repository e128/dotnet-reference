using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcreteTypeDiDependencyCodeFixBranchCoverageTests
{
    private static readonly ReferenceAssemblies References = TestReferences.Net100WithDependencyInjection;

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Singleton")]
    [InlineData("Scoped")]
    [InlineData("Transient")]
    public Task Fix_AddsConcreteAndForwardingRegistrations(string lifetime)
    {
        return CodeFixTestRunner.RunAsync<ConcreteTypeDiDependencyAnalyzer, ConcreteTypeDiDependencyCodeFixProvider>(
            $$"""
            using Microsoft.Extensions.DependencyInjection;
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
                    services.Add{{lifetime}}<IService, Service>();
                }
            }
            """,
            $$"""
            using Microsoft.Extensions.DependencyInjection;
            interface IService { }
            class Service : IService { }
            class Consumer
            {
                public Consumer(Service service) { }
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.Add{{lifetime}}<Service>();
                    services.Add{{lifetime}}<IService>(sp => sp.GetRequiredService<Service>());
                }
            }
            """,
            references: References,
            behaviors: CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            options: new CodeFixTestOptions { FixAllIterations = 1 });
    }
}
