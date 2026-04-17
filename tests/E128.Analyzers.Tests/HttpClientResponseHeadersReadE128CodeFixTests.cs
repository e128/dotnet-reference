using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientResponseHeadersReadE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<HttpClientResponseHeadersReadAnalyzer, HttpClientResponseHeadersReadCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_InsertsResponseHeadersRead_ForGetAsyncCall()
    {
        return VerifyFixAsync(
            """
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, CancellationToken ct)
                {
                    var response = await {|E128010:client.GetAsync("https://example.com", ct)|};
                }
            }
            """,
            """
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, CancellationToken ct)
                {
                    var response = await client.GetAsync("https://example.com", HttpCompletionOption.ResponseHeadersRead, ct);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_InsertsResponseHeadersRead_ForSendAsyncCall()
    {
        return VerifyFixAsync(
            """
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, CancellationToken ct)
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
                    var response = await {|E128010:client.SendAsync(request, ct)|};
                }
            }
            """,
            """
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, CancellationToken ct)
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                }
            }
            """);
    }
}
