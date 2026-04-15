using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DisposableSingletonFactoryE128CodeFixTests
{
    private static readonly ReferenceAssemblies Net100WithDi = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.6")]);

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<DisposableSingletonFactoryAnalyzer, DisposableSingletonFactoryCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = Net100WithDi,
            NumberOfFixAllIterations = 1,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RewritesToGenericOverload_WhenInterfaceAvailable()
    {
        return VerifyFixAsync(
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            interface IMyService { }
            class MyService : IMyService, IDisposable
            {
                public void Dispose() { }
            }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddSingleton({|E128031:sp => new MyService()|});
                }
            }
            """,
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            interface IMyService { }
            class MyService : IMyService, IDisposable
            {
                public void Dispose() { }
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
}
