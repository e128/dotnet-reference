using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcreteTypeDiDependencyAnalyzerBranchCoverageMoreTests
{
    private const string Services = """
                                    interface IMyService { }
                                    class MyService : IMyService { }
                                    class Services
                                    {
                                        public void AddSingleton(string name) { }
                                        public void AddSingleton<T>() { }
                                        public void AddSingleton<TService, TImpl>() { }
                                        public void AddSingleton<TA, TB, TC>() { }
                                    }
                                    """;

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenInterfaceRegistrationSitsBesideNonGenericAndUnresolvedCalls()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteTypeDiDependencyAnalyzer>(
            Services + """

            class Consumer
            {
                public Consumer({|E128035:MyService svc|}) { }
            }
            class Startup
            {
                void Configure(Services services, Missing missing)
                {
                    services.AddSingleton("name");
                    missing.AddSingleton<IMyService, MyService>();
                    services.AddSingleton<IMyService, MyService>();
                }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenRegistrationsUseArrayTypeArgumentsAndThreeTypeArguments()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteTypeDiDependencyAnalyzer>(
            Services + """

            class Consumer
            {
                public Consumer({|E128035:MyService svc|}, byte[] data) { }
            }
            class Startup
            {
                void Configure(Services services)
                {
                    services.AddSingleton<byte[]>();
                    services.AddSingleton<IMyService, byte[]>();
                    services.AddSingleton<IMyService, MyService, MyService>();
                    services.AddSingleton<IMyService, MyService>();
                }
            }
            """);
    }
}
