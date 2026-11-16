using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DirectHttpClientInstantiationCodeFixProviderBranchCoverageTests
{
    private static readonly ReferenceAssemblies Net100WithHttp = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.Http", "10.0.6")]);

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<DirectHttpClientInstantiationAnalyzer, DirectHttpClientInstantiationCodeFixProvider>(
            source,
            fixedCode,
            references: Net100WithHttp);
    }

    private static Task VerifyNoFixAsync(string code)
    {
        return CodeFixTestRunner.NoFixAsync<DirectHttpClientInstantiationAnalyzer, DirectHttpClientInstantiationCodeFixProvider>(
            code,
            references: Net100WithHttp);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AddsAConstructorBeforeTheFirstMethodAfterOtherMembers()
    {
        return VerifyFixAsync(
            """
            using System.Net.Http;
            class C
            {
                private readonly int _value;
                public int Value => _value;
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
                private readonly int _value;
                public int Value => _value;

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
    public Task Fix_ReusesAFactoryFieldAfterOtherMembers()
    {
        return VerifyFixAsync(
            """
            using System.Net.Http;
            class C
            {
                private readonly int _value;
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
                private readonly int _value;
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
    public Task NoFix_ForACreationInAStaticField()
    {
        return VerifyNoFixAsync("""
            using System.Net.Http;
            class C
            {
                private static readonly HttpClient Client = {|E128004:new HttpClient()|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_ForACreationInAStruct()
    {
        return VerifyNoFixAsync("""
            using System.Net.Http;
            struct C
            {
                HttpClient M() => {|E128004:new HttpClient()|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_ForACreationInARecord()
    {
        return VerifyNoFixAsync("""
            using System.Net.Http;
            record C
            {
                HttpClient M() => {|E128004:new HttpClient()|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_ForACreationInAStaticProperty()
    {
        return VerifyNoFixAsync("""
            using System.Net.Http;
            class C
            {
                public static HttpClient Client => {|E128004:new HttpClient()|};
            }
            """);
    }
}
