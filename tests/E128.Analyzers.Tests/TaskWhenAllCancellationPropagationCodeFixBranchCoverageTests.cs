using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the token rewrites of the cancellation propagation code fix.
/// </summary>
public sealed class TaskWhenAllCancellationPropagationCodeFixBranchCoverageTests
{
    private const string Header = """
                                  using System;
                                  using System.Collections.Generic;
                                  using System.Linq;
                                  using System.Net.Http;
                                  using System.Threading;
                                  using System.Threading.Tasks;

                                  """;

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AppendsTheTokenToEveryCallThatLacksIt_UsingTheParameterName()
    {
        return CodeFixTestRunner.RunAsync<TaskWhenAllCancellationPropagationAnalyzer, TaskWhenAllCancellationPropagationCodeFixProvider>(
            Header + """
                     class C
                     {
                         async Task M(IEnumerable<string> urls, HttpClient client, int retries, CancellationToken stopToken)
                         {
                             await {|E128038:Task.WhenAll(urls.Select(async url =>
                             {
                                 await client.GetAsync(url);
                                 await client.GetStringAsync(url, stopToken);
                                 await client.DeleteAsync(url);
                             }))|};
                         }
                     }
                     """,
            Header + """
                     class C
                     {
                         async Task M(IEnumerable<string> urls, HttpClient client, int retries, CancellationToken stopToken)
                         {
                             await Task.WhenAll(urls.Select(async url =>
                             {
                                 await client.GetAsync(url, stopToken);
                                 await client.GetStringAsync(url, stopToken);
                                 await client.DeleteAsync(url, stopToken);
                             }));
                         }
                     }
                     """);
    }
}
