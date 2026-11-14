using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpBodyReadWithoutSizeBoundAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStringReadHasNoBound()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>("""
                           using System.Net.Http;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(HttpResponseMessage response)
                               {
                                   _ = await {|E128227:response.Content.ReadAsStringAsync()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheByteArrayReadHasNoBound()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>("""
                           using System.Net.Http;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(HttpContent content)
                               {
                                   _ = await {|E128227:content.ReadAsByteArrayAsync()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenContentLengthIsCheckedFirst()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>("""
                           using System;
                           using System.Net.Http;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(HttpResponseMessage response)
                               {
                                   if (response.Content.Headers.ContentLength > 4096)
                                   {
                                       throw new InvalidOperationException();
                                   }

                                   _ = await response.Content.ReadAsStringAsync();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyIsBufferedWithACapFirst()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>("""
                           using System.Net.Http;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(HttpResponseMessage response)
                               {
                                   await response.Content.LoadIntoBufferAsync(4096);
                                   _ = await response.Content.ReadAsStringAsync();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeSetsMaxResponseContentBufferSize()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>("""
                           using System.Net.Http;
                           using System.Threading.Tasks;

                           class C
                           {
                               private readonly HttpClient _client = new() { MaxResponseContentBufferSize = 1 << 20 };

                               async Task M(HttpResponseMessage response)
                               {
                                   _ = await _client.SendAsync(new HttpRequestMessage());
                                   _ = await response.Content.ReadAsStringAsync();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheContentIsInMemory()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>("""
                           using System.Net.Http;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M()
                               {
                                   var content = new StringContent("{}");
                                   _ = await content.ReadAsStringAsync();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReadIsNotOnHttpContent()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>("""
                           using System.IO;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(StreamReader reader)
                               {
                                   _ = await reader.ReadToEndAsync();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnotherTypeDeclaresTheSameMethodName()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpBodyReadWithoutSizeBoundAnalyzer>("""
                           class Fake
                           {
                               internal string ReadAsStringAsync() => string.Empty;
                           }

                           class C
                           {
                               void M(Fake fake)
                               {
                                   _ = fake.ReadAsStringAsync();
                               }
                           }
                           """);
    }
}
