using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcreteOnlyDiRegistrationE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<ConcreteOnlyDiRegistrationAnalyzer, ConcreteOnlyDiRegistrationCodeFixProvider>(
            source,
            fixedCode,
            references: TestReferences.Net100WithDependencyInjection);
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

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RewritesToInterfaceMapped_GenericConcreteType()
    {
        // A simple type name drops the type arguments here, so the emitted call would not compile.
        return VerifyFixAsync(
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IRepository<T> { }
            class Repository<T> : IRepository<T> { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.{|E128032:AddSingleton<Repository<int>>()|};
                }
            }
            """,
            """
            using Microsoft.Extensions.DependencyInjection;
            interface IRepository<T> { }
            class Repository<T> : IRepository<T> { }
            class Startup
            {
                void Configure(IServiceCollection services)
                {
                    services.AddSingleton<IRepository<int>, Repository<int>>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_NotOffered_WhenTheFactoryPassesAnArgument()
    {
        const string source = """
                              using Microsoft.Extensions.DependencyInjection;
                              interface IRepository<T> { }
                              class Repository<T> : IRepository<T>
                              {
                                  public Repository(int capacity) { }
                              }
                              class Startup
                              {
                                  void Configure(IServiceCollection services)
                                  {
                                      services.{|E128032:AddSingleton<Repository<int>>(sp => new Repository<int>(1))|};
                                  }
                              }
                              """;

        // The two-type-argument overload takes no factory, so the registration must stay as it is.
        return CodeFixTestRunner.RunAsync<ConcreteOnlyDiRegistrationAnalyzer, ConcreteOnlyDiRegistrationCodeFixProvider>(
            source,
            source,
            references: TestReferences.Net100WithDependencyInjection,
            options: new CodeFixTestOptions { FixAllInDocumentIterations = 0 });
    }
}
