using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DirectHttpClientInstantiationCodeFixProviderBranchCoverageMoreTests
{
    private static readonly ReferenceAssemblies Net100WithHttp = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.Http", "10.0.6")]);

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AppendsTheFieldAndConstructorWhenTheClassHasNoMethod()
    {
        return CodeFixTestRunner.RunAsync<DirectHttpClientInstantiationAnalyzer, DirectHttpClientInstantiationCodeFixProvider>(
            """
            using System.Net.Http;
            class C
            {
                public HttpClient Client => {|E128004:new HttpClient()|};
            }
            """,
            """
            using System.Net.Http;
            class C
            {
                public HttpClient Client => _httpClientFactory.CreateClient();

                private readonly IHttpClientFactory _httpClientFactory;

                public C(IHttpClientFactory httpClientFactory)
                {
                    _httpClientFactory = httpClientFactory;
                }
            }
            """,
            references: Net100WithHttp);
    }
}
