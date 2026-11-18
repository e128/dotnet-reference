using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpRequestInLoopAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsync_InForeach_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpRequestInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Net.Http;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, IReadOnlyList<string> urls)
                {
                    foreach (string url in urls)
                    {
                        await {|E128203:client.GetAsync(url)|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SendAsync_InForLoop_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpRequestInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Net.Http;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, IReadOnlyList<HttpRequestMessage> requests)
                {
                    for (int i = 0; i < requests.Count; i++)
                    {
                        await {|E128203:client.SendAsync(requests[i])|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsync_OutsideLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpRequestInLoopAnalyzer>("""
            using System.Net.Http;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client)
                {
                    await client.GetAsync("https://example.test/items");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsync_ConstantBoundForLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpRequestInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Net.Http;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, IReadOnlyList<string> urls)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        await client.GetAsync(urls[i]);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsync_ConstantFieldBoundForLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpRequestInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Net.Http;
            using System.Threading.Tasks;
            class C
            {
                private const int MaxRedirectHops = 5;

                async Task M(HttpClient client, IReadOnlyList<string> urls)
                {
                    for (int hop = 0; hop <= MaxRedirectHops; hop++)
                    {
                        await client.GetAsync(urls[hop]);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsync_LocalConstantBoundForLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpRequestInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Net.Http;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, IReadOnlyList<string> urls)
                {
                    const int Hops = 3;
                    for (int hop = 0; hop <= Hops; hop++)
                    {
                        await client.GetAsync(urls[hop]);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsync_PropertyBoundForLoop_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpRequestInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Net.Http;
            using System.Threading.Tasks;
            class Options
            {
                public int MaxPages { get; set; }
            }

            class C
            {
                async Task M(HttpClient client, Options options, IReadOnlyList<string> urls)
                {
                    for (int page = 0; page < options.MaxPages; page++)
                    {
                        await {|E128203:client.GetAsync(urls[page])|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsync_InsideLambdaInLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpRequestInLoopAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Net.Http;
            using System.Threading.Tasks;
            class C
            {
                void M(HttpClient client, IReadOnlyList<string> urls)
                {
                    foreach (string url in urls)
                    {
                        Func<Task> request = () => client.GetAsync(url);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsync_InRetryLoopWithDelay_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpRequestInLoopAnalyzer>("""
            using System;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, CancellationToken token)
                {
                    while (!token.IsCancellationRequested)
                    {
                        HttpResponseMessage response = await client.GetAsync("/health", token);
                        if (response.IsSuccessStatusCode)
                        {
                            return;
                        }

                        await Task.Delay(500, token);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsync_OnUnrelatedType_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpRequestInLoopAnalyzer>("""
            using System.Collections.Generic;
            using System.Threading.Tasks;
            class Client
            {
                public Task GetAsync(string url) => Task.CompletedTask;
            }

            class C
            {
                async Task M(Client client, IReadOnlyList<string> urls)
                {
                    foreach (string url in urls)
                    {
                        await client.GetAsync(url);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task GetAsync_InsideLocalFunction_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpRequestInLoopAnalyzer>("""
            using System.Net.Http;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, string url, bool more)
                {
                    while (more)
                    {
                        void Local()
                        {
                            client.GetAsync(url);
                        }

                        await Task.Yield();
                        Local();
                    }
                }
            }
            """);
    }
}
