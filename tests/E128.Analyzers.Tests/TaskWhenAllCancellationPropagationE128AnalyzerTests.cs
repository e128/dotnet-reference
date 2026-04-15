using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskWhenAllCancellationPropagationE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TaskWhenAllCancellationPropagationAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_HttpClient_NoCt_FiresE128038()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Linq;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, CancellationToken ct)
                {
                    var urls = new List<string> { "http://a", "http://b" };
                    await {|E128038:Task.WhenAll(urls.Select(async url => await client.GetAsync(url)))|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_HttpClient_WithCt_DoesNotFire()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Linq;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, CancellationToken ct)
                {
                    var urls = new List<string> { "http://a", "http://b" };
                    await Task.WhenAll(urls.Select(async url => await client.GetAsync(url, ct)));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_NoCancellationTokenParam_DoesNotFire()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Linq;
            using System.Net.Http;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client)
                {
                    var urls = new List<string> { "http://a", "http://b" };
                    await Task.WhenAll(urls.Select(async url => await client.GetAsync(url)));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_NonHttpClientMethod_DoesNotFire()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                Task<int> ComputeAsync(string input) => Task.FromResult(input.Length);
                async Task M(CancellationToken ct)
                {
                    var items = new List<string> { "a", "b" };
                    await Task.WhenAll(items.Select(async x => await ComputeAsync(x)));
                }
            }
            """);
    }
}
