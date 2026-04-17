using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DirectHttpClientInstantiationAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<DirectHttpClientInstantiationAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NewHttpClientNoArgs_Fires()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
