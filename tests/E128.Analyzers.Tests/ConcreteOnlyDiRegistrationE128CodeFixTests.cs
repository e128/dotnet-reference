using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcreteOnlyDiRegistrationE128CodeFixTests
{
    private static readonly ReferenceAssemblies Net80WithDi = ReferenceAssemblies.Net.Net80
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "8.0.0")]);

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<ConcreteOnlyDiRegistrationAnalyzer, ConcreteOnlyDiRegistrationCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = Net80WithDi,
            NumberOfFixAllIterations = 1,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RewritesToInterfaceMapped_AddSingleton()
    {
        return VerifyFixAsync(
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IMyService { }
            class MyService : IMyService { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.{|E128032:AddSingleton<MyService>()|};
                }
            }
            """,
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IMyService { }
            class MyService : IMyService { }
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
    public Task CodeFix_RewritesToInterfaceMapped_AddScoped()
    {
        return VerifyFixAsync(
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IMyService { }
            class MyService : IMyService { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.{|E128032:AddScoped<MyService>()|};
                }
            }
            """,
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IMyService { }
            class MyService : IMyService { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddScoped<IMyService, MyService>();
                }
            }
            """);
    }
}
