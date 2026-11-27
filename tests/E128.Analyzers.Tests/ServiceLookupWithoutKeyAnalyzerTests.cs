using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ServiceLookupWithoutKeyAnalyzerTests
{
    private const string Stubs = """
        using System.Linq;
        using Microsoft.Extensions.DependencyInjection;

        namespace Microsoft.Extensions.DependencyInjection
        {
            public static class ServiceProviderServiceExtensions
            {
                public static System.Collections.Generic.IEnumerable<T> GetServices<T>(this System.IServiceProvider provider) => null;
            }
        }

        """;

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenFirstFiltersServices()
    {
        return AnalyzerTestRunner.VerifyAsync<ServiceLookupWithoutKeyAnalyzer>(Stubs + """
            class C
            {
                void Run(System.IServiceProvider provider)
                {
                    var client = [|provider.GetServices<object>().First(candidate => candidate is not null)|];
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenSingleFiltersServices()
    {
        return AnalyzerTestRunner.VerifyAsync<ServiceLookupWithoutKeyAnalyzer>(Stubs + """
            class C
            {
                void Run(System.IServiceProvider provider)
                {
                    var client = [|provider.GetServices<object>().Single(candidate => candidate is not null)|];
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenFirstHasNoPredicate()
    {
        return AnalyzerTestRunner.VerifyAsync<ServiceLookupWithoutKeyAnalyzer>(Stubs + """
            class C
            {
                void Run(System.IServiceProvider provider)
                {
                    var client = [|provider.GetServices<object>().First()|];
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLookupUsesFirstOrDefault()
    {
        return AnalyzerTestRunner.VerifyAsync<ServiceLookupWithoutKeyAnalyzer>(Stubs + """
            class C
            {
                void Run(System.IServiceProvider provider)
                {
                    var client = provider.GetServices<object>().FirstOrDefault(candidate => candidate is not null);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsNotAServiceCollection()
    {
        return AnalyzerTestRunner.VerifyAsync<ServiceLookupWithoutKeyAnalyzer>(Stubs + """
            class C
            {
                void Run()
                {
                    var items = new object[] { new object() };
                    var first = items.First(candidate => candidate is not null);
                }
            }
            """);
    }
}
