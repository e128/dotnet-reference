using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskWhenAllCancellationPropagationE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<TaskWhenAllCancellationPropagationAnalyzer, TaskWhenAllCancellationPropagationCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsCancellationToken_ToHttpClientCall()
    {
        const string source = """
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
            """;

        const string fixedCode = """
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
            """;

        return VerifyFixAsync(source, fixedCode);
    }
}
