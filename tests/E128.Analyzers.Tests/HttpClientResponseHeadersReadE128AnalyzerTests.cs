using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientResponseHeadersReadE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<HttpClientResponseHeadersReadAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsyncCall_WithoutCompletionOption_Fires_E128010()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SendAsyncCall_WithoutCompletionOption_Fires_E128010()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsyncCall_UriOnly_Fires_E128010()
    {
        return VerifyAsync("""
            using System.Net.Http;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client)
                {
                    var response = await {|E128010:client.GetAsync("https://example.com")|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsyncCall_WithResponseHeadersRead_NoDiagnostic()
    {
        return VerifyAsync("""
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
    public Task NonHttpClient_NoDiagnostic()
    {
        return VerifyAsync("""
            using System.Threading.Tasks;
            class FakeClient
            {
                public Task<string> GetAsync(string url) => Task.FromResult(url);
            }
            class C
            {
                async Task M()
                {
                    var client = new FakeClient();
                    var result = await client.GetAsync("https://example.com");
                }
            }
            """);
    }
}
