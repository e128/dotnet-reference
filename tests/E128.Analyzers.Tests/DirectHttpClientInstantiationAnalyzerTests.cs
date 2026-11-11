using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DirectHttpClientInstantiationAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task NewHttpClientNoArgs_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<DirectHttpClientInstantiationAnalyzer>("""
                           using System.Net.Http;
                           class C
                           {
                               void M()
                               {
                                   var client = {|E128004:new HttpClient()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NewHttpClientNoArgs_AsField_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<DirectHttpClientInstantiationAnalyzer>("""
                           using System.Net.Http;
                           class C
                           {
                               private readonly HttpClient _client = {|E128004:new HttpClient()|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NewHttpClientWithHandler_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<DirectHttpClientInstantiationAnalyzer>("""
                           using System.Net.Http;
                           class C
                           {
                               void M()
                               {
                                   var handler = new HttpClientHandler();
                                   var client = new HttpClient(handler);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UserDefinedHttpClient_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<DirectHttpClientInstantiationAnalyzer>("""
                           class HttpClient
                           {
                               public HttpClient() { }
                           }
                           class C
                           {
                               void M()
                               {
                                   var client = new HttpClient();
                               }
                           }
                           """);
    }
}
