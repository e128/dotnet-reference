using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcreteTypeDiDependencyCodeFixProviderBranchCoverageMoreTests
{
    private static readonly ReferenceAssemblies References = TestReferences.Net100WithDependencyInjection;

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheRegistrationNamesTheImplementationWithAQualifier()
    {
        return CodeFixTestRunner.NoFixAsync<ConcreteTypeDiDependencyAnalyzer, ConcreteTypeDiDependencyCodeFixProvider>(
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IService { }
            class Outer
            {
                public class Service : IService { }
            }
            class Consumer
            {
                public Consumer({|E128035:Outer.Service service|}) { }
            }
            class Startup
            {
                void Note<T>() { }

                void Configure(IServiceCollection services)
                {
                    this.Note<int>();
                    services.AddSingleton<IService, Outer.Service>();
                }
            }
            """,
            References,
            CodeFixTestBehaviors.SkipLocalDiagnosticCheck);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheRegistrationIsNotAStatement()
    {
        return CodeFixTestRunner.NoFixAsync<ConcreteTypeDiDependencyAnalyzer, ConcreteTypeDiDependencyCodeFixProvider>(
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IService { }
            class Service : IService { }
            class Consumer
            {
                public Consumer({|E128035:Service service|}) { }
            }
            class Startup
            {
                IServiceCollection Configure(IServiceCollection services) =>
                    services.AddSingleton<IService, Service>();
            }
            """,
            References,
            CodeFixTestBehaviors.SkipLocalDiagnosticCheck);
    }
}
