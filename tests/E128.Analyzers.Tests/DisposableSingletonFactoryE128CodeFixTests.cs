using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DisposableSingletonFactoryE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<DisposableSingletonFactoryAnalyzer, DisposableSingletonFactoryCodeFixProvider>(
            source,
            fixedCode,
            references: TestReferences.Net100WithDependencyInjection);
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

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_WritesTheGenericInterface_WhenTheServiceIsGeneric()
    {
        return VerifyFixAsync(
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;

            namespace MyApp
            {
                interface IMyService<T> { }
                class MyService<T> : IMyService<T>, IDisposable
                {
                    public void Dispose() { }
                }
                class Startup
                {
                    void Configure(IServiceCollection services)
                    {
                        services.AddSingleton({|E128031:sp => new MyService<string>()|});
                    }
                }
            }
            """,
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;

            namespace MyApp
            {
                interface IMyService<T> { }
                class MyService<T> : IMyService<T>, IDisposable
                {
                    public void Dispose() { }
                }
                class Startup
                {
                    void Configure(IServiceCollection services)
                    {
                        services.AddSingleton<IMyService<string>, MyService<string>>();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_NotOffered_WhenTheFactoryPassesAnArgument()
    {
        const string source = """
                              using System;
                              using Microsoft.Extensions.DependencyInjection;

                              namespace MyApp
                              {
                                  interface IMyService<T> { }
                                  class MyService<T> : IMyService<T>, IDisposable
                                  {
                                      public MyService(int capacity) { }
                                      public void Dispose() { }
                                  }
                                  class Startup
                                  {
                                      void Configure(IServiceCollection services)
                                      {
                                          services.AddSingleton<MyService<string>>({|E128031:sp => new MyService<string>(1)|});
                                      }
                                  }
                              }
                              """;

        // The container cannot rebuild a type that the factory builds with an argument.
        return CodeFixTestRunner.RunAsync<DisposableSingletonFactoryAnalyzer, DisposableSingletonFactoryCodeFixProvider>(
            source,
            source,
            references: TestReferences.Net100WithDependencyInjection,
            options: new CodeFixTestOptions { FixAllInDocumentIterations = 0 });
    }
}
