using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the lambda shapes the cancellation propagation rule and its code fix inspect.
/// </summary>
public sealed class TaskWhenAllCancellationPropagationBranchCoverageTests
{
    private const string Header = """
                                  using System;
                                  using System.Collections.Generic;
                                  using System.Linq;
                                  using System.Net.Http;
                                  using System.Threading;
                                  using System.Threading.Tasks;

                                  class PageOptions
                                  {
                                      public CancellationToken CancellationToken { get; set; }

                                      public int Timeout { get; set; }
                                  }

                                  class Page
                                  {
                                      public Task GotoAsync(string url, PageOptions? options = null) => Task.CompletedTask;

                                      public Task ClickAsync(string selector, CancellationToken token) => Task.CompletedTask;
                                  }

                                  """;

    private static Task VerifyAsync(string source, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<TaskWhenAllCancellationPropagationAnalyzer>(Header + source, expected: null, options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAPlaywrightCallWithoutAToken()
    {
        return VerifyAsync("""
                           class C
                           {
                               async Task M(IEnumerable<string> urls, Page page, CancellationToken ct)
                               {
                                   await {|E128038:Task.WhenAll(urls.Select(async url => await page.GotoAsync(url)))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheOptionsObjectCarriesTheToken()
    {
        return VerifyAsync("""
                           class C
                           {
                               async Task M(IEnumerable<string> urls, Page page, CancellationToken ct)
                               {
                                   await Task.WhenAll(urls.Select(async url => await page.GotoAsync(url, new PageOptions { CancellationToken = ct })));
                                   await Task.WhenAll(urls.Select(async url => await page.GotoAsync(url, new() { CancellationToken = ct })));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheOptionsObjectCarriesNoToken()
    {
        return VerifyAsync("""
                           class C
                           {
                               async Task M(IEnumerable<string> urls, Page page, CancellationToken ct)
                               {
                                   await {|E128038:Task.WhenAll(urls.Select(async url => await page.GotoAsync(url, new PageOptions { Timeout = 5 })))|};
                                   await {|E128038:Task.WhenAll(urls.Select(async url => await page.GotoAsync(url, new PageOptions())))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTokenIsAPositionalArgument()
    {
        return VerifyAsync("""
                           class C
                           {
                               async Task M(IEnumerable<string> urls, Page page, CancellationToken ct)
                               {
                                   await Task.WhenAll(urls.Select(async url => await page.ClickAsync(url, ct)));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenWhenAllTakesSeveralArguments()
    {
        return VerifyAsync("""
                           class C
                           {
                               async Task M(Task first, Task second, CancellationToken ct)
                               {
                                   await Task.WhenAll(first, second);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenWhenAllTakesAVariable()
    {
        return VerifyAsync("""
                           class C
                           {
                               async Task M(IEnumerable<Task> tasks, CancellationToken ct)
                               {
                                   await Task.WhenAll(tasks);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsNotASelectCall()
    {
        return VerifyAsync("""
                           class C
                           {
                               static IEnumerable<Task> Select(IEnumerable<string> urls) => Array.Empty<Task>();

                               async Task M(IEnumerable<string> urls, IEnumerable<Task> tasks, CancellationToken ct)
                               {
                                   await Task.WhenAll(Select(urls));
                                   await Task.WhenAll(tasks.Where(task => task.IsCompleted));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSelectLambdaIsNotAsync()
    {
        return VerifyAsync("""
                           class C
                           {
                               async Task M(IEnumerable<string> urls, HttpClient client, CancellationToken ct)
                               {
                                   await Task.WhenAll(urls.Select(url => client.GetAsync(url)));
                                   await Task.WhenAll(urls.Select((url) => client.GetAsync(url)));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSelectorIsAMethodGroup()
    {
        return VerifyAsync("""
                           class C
                           {
                               static Task Fetch(string url) => Task.CompletedTask;

                               async Task M(IEnumerable<string> urls, CancellationToken ct)
                               {
                                   await Task.WhenAll(urls.Select(Fetch));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSelectHasNoArguments()
    {
        return VerifyAsync(
            """
            class C
            {
                async Task M(IEnumerable<string> urls, CancellationToken ct)
                {
                    await Task.WhenAll(urls.Select());
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNoMethodEncloses_TheWhenAll()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static readonly Func<IEnumerable<string>, HttpClient, Task> Run =
                                   (urls, client) => Task.WhenAll(urls.Select(async url => await client.GetAsync(url)));
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTokenTypeDoesNotBind()
    {
        return VerifyAsync(
            """
            class C
            {
                async Task M(IEnumerable<string> urls, HttpClient client, MissingToken ct)
                {
                    await Task.WhenAll(urls.Select(async url => await client.GetAsync(url)));
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLambdaCallsAnUnqualifiedOrGenericMethod()
    {
        return VerifyAsync("""
                           class C
                           {
                               static Task<string> GetAsync(string url) => Task.FromResult(url);

                               static Task<T> Fetch<T>(string url) => Task.FromResult(default(T)!);

                               async Task M(IEnumerable<string> urls, HttpClient client, CancellationToken ct)
                               {
                                   await Task.WhenAll(urls.Select(async url => await GetAsync(url)));
                                   await Task.WhenAll(urls.Select(async url => await Fetch<int>(url)));
                               }
                           }
                           """);
    }
}
