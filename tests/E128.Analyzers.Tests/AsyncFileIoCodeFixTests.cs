using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncFileIoCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<AsyncFileIoAnalyzer, AsyncFileIoCodeFixProvider>(
            source,
            fixedCode,
            markupOptions: MarkupOptions.UseFirstDescriptor,
            options: new CodeFixTestOptions { FixAllIterations = 1 });
    }

    /// <summary>
    ///     AC-4: fixing a void sync method promotes it to async Task and awaits the Async sibling.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncFileIoCodeFix_PromotesVoidMethodToAsyncTask_WhenFixApplied()
    {
        const string source = """
                              using System.IO;
                              class C
                              {
                                  void M(string path, string text)
                                  {
                                      {|E128092:File.WriteAllText(path, text)|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.IO;
                                 using System.Threading.Tasks;

                                 class C
                                 {
                                     async Task M(string path, string text)
                                     {
                                         await File.WriteAllTextAsync(path, text);
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    /// <summary>
    ///     AC-5: fixing a value-returning sync method promotes it to async Task&lt;T&gt; and awaits the Async sibling.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncFileIoCodeFix_PromotesReturningMethodToAsyncTaskOfT_WhenFixApplied()
    {
        const string source = """
                              using System.IO;
                              class C
                              {
                                  string M(string path)
                                  {
                                      return {|E128092:File.ReadAllText(path)|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.IO;
                                 using System.Threading.Tasks;

                                 class C
                                 {
                                     async Task<string> M(string path)
                                     {
                                         return await File.ReadAllTextAsync(path);
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
