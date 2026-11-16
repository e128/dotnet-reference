using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DisposableSingletonFactoryBranchCoverageTests
{
    private const string Prefix = """
                                  using System;

                                  class MyService : IDisposable
                                  {
                                      public void Dispose() { }
                                  }

                                  """;

    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableSingletonFactoryAnalyzer>(
            Prefix + "class Startup\n{\n    static void AddSingleton(Func<IServiceProvider, object?> factory) { }\n\n" + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnUnqualifiedAddSingletonCall()
    {
        return VerifyAsync("""
                void Configure()
                {
                    AddSingleton({|E128031:sp => new MyService()|});
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFactoryIsAMethodGroup()
    {
        return VerifyAsync("""
                static object? Build(IServiceProvider provider) => new MyService();

                void Configure()
                {
                    AddSingleton(Build);
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLambdaHasABlockBody()
    {
        return VerifyAsync("""
                void Configure()
                {
                    AddSingleton(sp => { return new MyService(); });
                    AddSingleton((IServiceProvider sp) => { return new MyService(); });
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLambdaBodyHasNoType()
    {
        return VerifyAsync("""
                void Configure()
                {
                    AddSingleton(sp => null);
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnInvokedResultAndForOtherMethodNames()
    {
        return VerifyAsync("""
                static Action<Func<IServiceProvider, object?>> Pick() => static _ => { };

                static void AddScoped(Func<IServiceProvider, object?> factory) { }

                void Configure()
                {
                    Pick()(sp => new MyService());
                    AddScoped(sp => new MyService());
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAddSingletonTakesTwoArguments()
    {
        return AnalyzerTestRunner.VerifyAsync<DisposableSingletonFactoryAnalyzer>(
            Prefix + """
            class Startup
            {
                static void AddSingleton(Func<IServiceProvider, object?> factory, int lifetime) { }

                void Configure()
                {
                    AddSingleton(sp => new MyService(), 1);
                }
            }
            """);
    }
}
