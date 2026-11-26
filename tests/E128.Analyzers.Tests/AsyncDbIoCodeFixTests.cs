using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncDbIoCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<AsyncDbIoAnalyzer, AsyncDbIoCodeFixProvider>(
            TestStubs.AsyncDbIoFakeConnection + "\n" + source,
            TestStubs.AsyncDbIoFakeConnectionWithTaskUsing + "\n" + fixedCode,
            markupOptions: MarkupOptions.UseFirstDescriptor,
            options: new CodeFixTestOptions { FixAllIterations = 1 });
    }

    /// <summary>
    ///     AC-4: fixing a void sync method promotes it to async Task and awaits the Async sibling.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncDbIoCodeFix_PromotesVoidMethodToAsyncTask_WhenFixApplied()
    {
        const string source = """
                              class C
                              {
                                  void M(FakeConnection connection)
                                  {
                                      {|E128093:connection.Open()|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 class C
                                 {
                                     async Task M(FakeConnection connection)
                                     {
                                         await connection.OpenAsync();
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
