using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcreteOnlyDiRegistrationCodeFixProviderBranchCoverageTests
{
    private static readonly ReferenceAssemblies Net100WithDi = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.DependencyInjection", "10.0.6")]);

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<ConcreteOnlyDiRegistrationAnalyzer, ConcreteOnlyDiRegistrationCodeFixProvider>(
            source,
            fixedCode,
            references: Net100WithDi);
    }

    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<ConcreteOnlyDiRegistrationAnalyzer, ConcreteOnlyDiRegistrationCodeFixProvider>(
            source,
            references: Net100WithDi);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RewritesAParameterlessFactory()
    {
        return VerifyFixAsync(
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IService { }
            class Service : IService { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.{|E128032:AddSingleton<Service>(sp => new Service())|};
                }
            }
            """,
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IService { }
            class Service : IService { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddSingleton<IService, Service>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RewritesAParenthesizedFactoryLambda()
    {
        return VerifyFixAsync(
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IService { }
            class Service : IService { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.{|E128032:AddScoped<Service>((sp) => new Service())|};
                }
            }
            """,
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IService { }
            class Service : IService { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddScoped<IService, Service>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_ForAFactoryThatUsesABlockBody()
    {
        return VerifyNoFixAsync("""
            using Microsoft.Extensions.DependencyInjection;
            interface IService { }
            class Service : IService { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.{|E128032:AddSingleton<Service>(sp => { return new Service(); })|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_ForAFactoryMethodGroup()
    {
        return VerifyNoFixAsync("""
            using System;
            using Microsoft.Extensions.DependencyInjection;
            interface IService { }
            class Service : IService { }
            class Startup
            {
                Service Create(IServiceProvider provider) => new();
                void Configure(IServiceCollection services)
                {
                    services.{|E128032:AddSingleton<Service>(Create)|};
                }
            }
            """);
    }

}
