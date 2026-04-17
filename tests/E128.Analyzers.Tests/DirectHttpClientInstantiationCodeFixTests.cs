using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DirectHttpClientInstantiationCodeFixTests
{
    private static readonly ReferenceAssemblies Net100WithHttp = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.Http", "10.0.6")]);

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<DirectHttpClientInstantiationAnalyzer, DirectHttpClientInstantiationCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = Net100WithHttp,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesNewHttpClient_WithFactoryCreateClient()
    {
        return VerifyFixAsync(
            """
            using System.Net.Http;
            class C
            {
                void M()
                {
                    var client = {|E128004:new HttpClient()|};
                }
            }
            """,
            """
            using System.Net.Http;
            class C
            {
                private readonly IHttpClientFactory _httpClientFactory;

                public C(IHttpClientFactory httpClientFactory)
                {
                    _httpClientFactory = httpClientFactory;
                }
                void M()
                {
                    var client = _httpClientFactory.CreateClient();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReusesExistingFactory_WhenFieldExists()
    {
        return VerifyFixAsync(
            """
            using System.Net.Http;
            class C
            {
                private readonly IHttpClientFactory _factory;

                public C(IHttpClientFactory factory)
                {
                    _factory = factory;
                }

                void M()
                {
                    var client = {|E128004:new HttpClient()|};
                }
            }
            """,
            """
            using System.Net.Http;
            class C
            {
                private readonly IHttpClientFactory _factory;

                public C(IHttpClientFactory factory)
                {
                    _factory = factory;
                }

                void M()
                {
                    var client = _factory.CreateClient();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsConstructorParameter_WhenConstructorExists()
    {
        return VerifyFixAsync(
            """
            using System.Net.Http;
            class C
            {
                private readonly string _name;

                public C(string name)
                {
                    _name = name;
                }

                void M()
                {
                    var client = {|E128004:new HttpClient()|};
                }
            }
            """,
            """
            using System.Net.Http;
            class C
            {
                private readonly string _name;
                private readonly IHttpClientFactory _httpClientFactory;

                public C(string name, IHttpClientFactory httpClientFactory)
                {
                    _name = name;
                    _httpClientFactory = httpClientFactory;
                }

                void M()
                {
                    var client = _httpClientFactory.CreateClient();
                }
            }
            """);
    }
}
