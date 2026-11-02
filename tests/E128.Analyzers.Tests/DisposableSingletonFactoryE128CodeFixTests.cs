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

    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<DisposableSingletonFactoryAnalyzer, DisposableSingletonFactoryCodeFixProvider>(source, fixedCode, references: Net100WithDi);

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
        var test = new CSharpCodeFixTest<
            DisposableSingletonFactoryAnalyzer,
            DisposableSingletonFactoryCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = Net100WithDi,
            NumberOfFixAllInDocumentIterations = 0
        };

        return test.RunAsync();
    }
}
