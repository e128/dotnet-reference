using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
/// E128035 uses CompilationEnd (non-local diagnostics) and the code fix modifies a different
/// location than the diagnostic. The standard code fix test framework does not support non-local
/// fixes. These tests verify the analyzer fires correctly via the analyzer test harness.
/// </summary>
public sealed class ConcreteTypeDiDependencyE128CodeFixTests
{
    private static readonly ReferenceAssemblies Net80WithDi = ReferenceAssemblies.Net.Net80
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "8.0.0")]);

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConcreteTypeDiDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = Net80WithDi,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AnalyzerFires_WhenConcreteParamOnlyInterfaceRegistered()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.DependencyInjection;
            interface IMyService { }
            class MyService : IMyService { }
            class Consumer
            {
                public Consumer({|E128035:MyService svc|}) { }
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddSingleton<IMyService, MyService>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AnalyzerDoesNotFire_WhenDirectRegistrationExists()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.DependencyInjection;
            interface IMyService { }
            class MyService : IMyService { }
            class Consumer
            {
                public Consumer(MyService svc) { }
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddSingleton<IMyService, MyService>();
                    services.AddSingleton<MyService>();
                }
            }
            """);
    }
}
