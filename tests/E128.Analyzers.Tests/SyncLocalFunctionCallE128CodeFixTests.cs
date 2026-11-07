using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SyncLocalFunctionCallE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<SyncLocalFunctionCallAnalyzer, SyncLocalFunctionCallCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsTaskUsing_WhenFullyQualified()
    {
        const string source = """
                              class C
                              {
                                  void M()
                                  {
                                      async System.Threading.Tasks.Task<string> FetchAsync() =>
                                          await System.Threading.Tasks.Task.FromResult("value");
                                      _ = {|E128096:FetchAsync()|}.Result;
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Threading.Tasks;

                                 class C
                                 {
                                     async Task M()
                                     {
                                         async System.Threading.Tasks.Task<string> FetchAsync() =>
                                             await System.Threading.Tasks.Task.FromResult("value");
                                         _ = await FetchAsync();
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesWaitWithAwait()
    {
        const string source = """
                              using System.Threading.Tasks;
                              class C
                              {
                                  void M()
                                  {
                                      async Task LoadAsync() { await Task.CompletedTask; }
                                      {|E128096:LoadAsync()|}.Wait();
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                     async Task M()
                                     {
                                         async Task LoadAsync() { await Task.CompletedTask; }
                                         await LoadAsync();
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
