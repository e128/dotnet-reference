using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientMissingOceCatchE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<HttpClientMissingOceCatchAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BroadCatchWithoutOce_Fires()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
}
