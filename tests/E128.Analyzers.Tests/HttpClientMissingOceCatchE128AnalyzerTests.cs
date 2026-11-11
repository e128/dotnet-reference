using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientMissingOceCatchE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task BroadCatchWithoutOce_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientMissingOceCatchAnalyzer>("""
                           using System;
                           using System.Net.Http;
                           using System.Threading.Tasks;

                           public class Service
                           {
                               private readonly HttpClient _client = new();

                               public async Task DoWork()
                               {
                                   try
                                   {
                                       await _client.GetAsync("https://example.com");
                                   }
                                   {|E128051:catch (Exception)|}
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OceCatchBeforeBroadCatch_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientMissingOceCatchAnalyzer>("""
                           using System;
                           using System.Net.Http;
                           using System.Threading;
                           using System.Threading.Tasks;

                           public class Service
                           {
                               private readonly HttpClient _client = new();

                               public async Task DoWork()
                               {
                                   try
                                   {
                                       await _client.GetAsync("https://example.com");
                                   }
                                   catch (OperationCanceledException)
                                   {
                                       throw;
                                   }
                                   catch (Exception)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonAsyncMethod_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientMissingOceCatchAnalyzer>("""
                           using System;
                           using System.Net.Http;
                           using System.Threading.Tasks;

                           public class Service
                           {
                               private readonly HttpClient _client = new();

                               public void DoWork()
                               {
                                   try
                                   {
                                       _client.GetAsync("https://example.com").Wait();
                                   }
                                   catch (Exception)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenFilteredCatch_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientMissingOceCatchAnalyzer>("""
                           using System;
                           using System.Net.Http;
                           using System.Threading.Tasks;

                           public class Service
                           {
                               private readonly HttpClient _client = new();

                               public async Task DoWork()
                               {
                                   try
                                   {
                                       await _client.GetAsync("https://example.com");
                                   }
                                   catch (Exception ex) when (ex is not OperationCanceledException)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BareCatchWithHttpClient_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientMissingOceCatchAnalyzer>("""
                           using System;
                           using System.Net.Http;
                           using System.Threading.Tasks;

                           public class Service
                           {
                               private readonly HttpClient _client = new();

                               public async Task DoWork()
                               {
                                   try
                                   {
                                       await _client.PostAsync("https://example.com", null);
                                   }
                                   {|E128051:catch|}
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CustomClientWithMatchingMethodNames_NoDiagnostic()
    {
        // A domain client that declares GetAsync does not throw the HttpClient cancellation this
        // rule is about.
        return AnalyzerTestRunner.VerifyAsync<HttpClientMissingOceCatchAnalyzer>("""
                           using System;
                           using System.Threading.Tasks;

                           public class UserClient
                           {
                               public Task<string> GetAsync(string url) => Task.FromResult(url);
                           }

                           public class Service
                           {
                               private readonly UserClient _client = new();

                               public async Task DoWork()
                               {
                                   try
                                   {
                                       await _client.GetAsync("https://example.com");
                                   }
                                   catch (Exception)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalTypeNamedException_NoDiagnostic()
    {
        // The catch handles one derived type, so it is narrower than catch (System.Exception).
        return AnalyzerTestRunner.VerifyAsync<HttpClientMissingOceCatchAnalyzer>("""
                           using System;
                           using System.Net.Http;
                           using System.Threading.Tasks;

                           public class Exception : System.Exception
                           {
                           }

                           public class Service
                           {
                               private readonly HttpClient _client = new();

                               public async Task DoWork()
                               {
                                   try
                                   {
                                       await _client.GetAsync("https://example.com");
                                   }
                                   catch (Exception)
                                   {
                                   }
                               }
                           }
                           """);
    }
}
