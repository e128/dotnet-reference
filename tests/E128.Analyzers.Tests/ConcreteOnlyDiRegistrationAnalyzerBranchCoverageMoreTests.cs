using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcreteOnlyDiRegistrationAnalyzerBranchCoverageMoreTests
{
    private const string Stubs = """
                                 interface IMyService { }
                                 class MyService : IMyService { }
                                 class Services
                                 {
                                     public Services AddSingleton<T>() where T : class => this;
                                     public T GetRequiredService<T>() where T : class => default!;
                                     public Services Configure() => this;
                                 }
                                 """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<ConcreteOnlyDiRegistrationAnalyzer>(code + "\n" + Stubs);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheRegistrationIsAnUnqualifiedCall()
    {
        return VerifyAsync(
            """
            class Startup
            {
                static void AddSingleton<T>(T instance) where T : class
                {
                }

                static void Configure()
                {
                    {|E128032:AddSingleton(new MyService())|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRegistrationUsesAConditionalAccess()
    {
        return VerifyAsync(
            """
            class Startup
            {
                static void Configure(Services? services)
                {
                    services?.AddSingleton<MyService>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRegistrationTargetIsDynamic()
    {
        return VerifyAsync(
            """
            class Startup
            {
                static void Configure(dynamic services)
                {
                    services.AddSingleton<MyService>();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheRegistrationSitsInAnExpressionBody()
    {
        return VerifyAsync(
            """
            class Startup
            {
                static Services Configure(Services services) => services.{|E128032:AddSingleton<MyService>()|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheScopeHoldsOtherCallsAndAForwardingCallForAnotherType()
    {
        return VerifyAsync(
            """
            class Other { }

            class Startup
            {
                static void Configure(Services services)
                {
                    services.Configure();
                    services.GetRequiredService<Other>();
                    services.{|E128032:AddSingleton<MyService>()|};
                }
            }
            """);
    }
}
