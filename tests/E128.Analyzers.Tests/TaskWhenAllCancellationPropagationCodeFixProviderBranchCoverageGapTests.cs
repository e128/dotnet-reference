using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskWhenAllCancellationPropagationCodeFixProviderBranchCoverageGapTests
{
    private const string Header = """
                                  using System;
                                  using System.Collections.Generic;
                                  using System.Linq;
                                  using System.Net.Http;
                                  using System.Threading;
                                  using System.Threading.Tasks;

                                  class Service
                                  {
                                      public Task GetAsync(string url) => Task.CompletedTask;

                                      public Task GetAsync(string url, CancellationToken token) => Task.CompletedTask;

                                      public Task GetAsync<T>(string url) => Task.CompletedTask;

                                      public Task PutAsync(string url, object? body) => Task.CompletedTask;

                                      public Task PutAsync(string url, object? body, CancellationToken token) => Task.CompletedTask;
                                  }

                                  """;

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_SkipsPlainAndGenericCallsInsideTheLambda()
    {
        return CodeFixTestRunner.RunAsync<TaskWhenAllCancellationPropagationAnalyzer, TaskWhenAllCancellationPropagationCodeFixProvider>(
            Header + """
                     class C
                     {
                         static Task Pause(string url) => Task.CompletedTask;

                         async Task M(IEnumerable<string> urls, Service service, CancellationToken stopToken)
                         {
                             await {|E128038:Task.WhenAll(urls.Select(async url =>
                             {
                                 await Pause(url);
                                 await service.GetAsync<int>(url);
                                 await service.GetAsync(url);
                             }))|};
                         }
                     }
                     """,
            Header + """
                     class C
                     {
                         static Task Pause(string url) => Task.CompletedTask;

                         async Task M(IEnumerable<string> urls, Service service, CancellationToken stopToken)
                         {
                             await Task.WhenAll(urls.Select(async url =>
                             {
                                 await Pause(url);
                                 await service.GetAsync<int>(url);
                                 await service.GetAsync(url, stopToken);
                             }));
                         }
                     }
                     """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AppendsTheTokenAfterANullArgument()
    {
        return CodeFixTestRunner.RunAsync<TaskWhenAllCancellationPropagationAnalyzer, TaskWhenAllCancellationPropagationCodeFixProvider>(
            Header + """
                     class C
                     {
                         async Task M(IEnumerable<string> urls, Service service, CancellationToken stopToken)
                         {
                             await {|E128038:Task.WhenAll(urls.Select(async url =>
                             {
                                 await service.PutAsync(url, null);
                             }))|};
                         }
                     }
                     """,
            Header + """
                     class C
                     {
                         async Task M(IEnumerable<string> urls, Service service, CancellationToken stopToken)
                         {
                             await Task.WhenAll(urls.Select(async url =>
                             {
                                 await service.PutAsync(url, null, stopToken);
                             }));
                         }
                     }
                     """);
    }
}
