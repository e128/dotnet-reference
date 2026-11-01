using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SyncOverAsyncCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<SyncOverAsyncAnalyzer, SyncOverAsyncCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesTaskResult_WithAwait()
    {
        const string source = """
                              using System.Threading.Tasks;
                              class C
                              {
                                  int M()
                                  {
                                      var task = Task.FromResult(42);
                                      return {|E128008:task.Result|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                     async Task<int> M()
                                     {
                                         var task = Task.FromResult(42);
                                         return await task;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesGetAwaiterGetResult_WithAwait()
    {
        const string source = """
                              using System.Threading.Tasks;
                              class C
                              {
                                  int M()
                                  {
                                      var task = Task.FromResult(42);
                                      return task.{|E128008:GetAwaiter|}().GetResult();
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                     async Task<int> M()
                                     {
                                         var task = Task.FromResult(42);
                                         return await task;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_PromotesVoidMethod_ToAsyncTask()
    {
        const string source = """
                              using System.Threading.Tasks;
                              class C
                              {
                                  void M()
                                  {
                                      var task = Task.FromResult(42);
                                      _ = {|E128008:task.Result|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                     async Task M()
                                     {
                                         var task = Task.FromResult(42);
                                         _ = await task;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsTaskUsing_WhenFullyQualified()
    {
        const string source = """
                              class C
                              {
                                  void M()
                                  {
                                      var task = System.Threading.Tasks.Task.FromResult(42);
                                      _ = {|E128008:task.Result|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Threading.Tasks;

                                 class C
                                 {
                                     async Task M()
                                     {
                                         var task = System.Threading.Tasks.Task.FromResult(42);
                                         _ = await task;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_OffersNoFix_WhenInsideALambda()
    {
        // A lambda cannot take the async modifier from this fix, so an await there does not compile.
        return VerifyNoFixAsync("""
                                using System;
                                using System.Threading.Tasks;

                                class C
                                {
                                    Func<int> M()
                                    {
                                        var task = Task.FromResult(42);
                                        return () => {|E128008:task.Result|};
                                    }
                                }
                                """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_OffersNoFix_WhenInsideAPropertyAccessor()
    {
        // A property accessor cannot take the async modifier from this fix either.
        return VerifyNoFixAsync("""
                                using System.Threading.Tasks;

                                class C
                                {
                                    private readonly Task<int> _task = Task.FromResult(42);
                                    int Value => {|E128008:_task.Result|};
                                }
                                """);
    }

    private static Task VerifyNoFixAsync(string code) =>
        CodeFixTestRunner.NoFixAsync<SyncOverAsyncAnalyzer, SyncOverAsyncCodeFixProvider>(code);
}
