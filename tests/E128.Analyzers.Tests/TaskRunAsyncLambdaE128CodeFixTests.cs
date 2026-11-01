using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskRunAsyncLambdaE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<TaskRunAsyncLambdaAnalyzer, TaskRunAsyncLambdaCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RemovesTaskRunWrapper_ExpressionBody()
    {
        const string source = """
                              using System.Threading.Tasks;
                              class C
                              {
                                  async Task M()
                                  {
                                      await {|E128036:Task.Run(async () => await Task.Delay(1))|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                     async Task M()
                                     {
                                         await Task.Delay(1);
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_NoFixOffered_ForBlockBody()
    {
        // Block-body lambdas are not auto-fixable; diagnostic still fires but no code fix.
        // FixedCode == TestCode verifies no fix was applied.
        return VerifyNoFixAsync("""
                                using System.Threading.Tasks;
                                class C
                                {
                                    async Task M()
                                    {
                                        await {|E128036:Task.Run(async () =>
                                        {
                                            await Task.Delay(1);
                                            await Task.Delay(2);
                                        })|};
                                    }
                                }
                                """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_NoFixOffered_ForCancellationTokenArgument()
    {
        // Removing the wrapper would drop the cancellation token, so the work would ignore it.
        return VerifyNoFixAsync("""
                                using System.Threading;
                                using System.Threading.Tasks;
                                class C
                                {
                                    async Task M()
                                    {
                                        await {|E128036:Task.Run(async () => await Task.Delay(1), CancellationToken.None)|};
                                    }
                                }
                                """);
    }

    private static Task VerifyNoFixAsync(string code) =>
        CodeFixTestRunner.NoFixAsync<TaskRunAsyncLambdaAnalyzer, TaskRunAsyncLambdaCodeFixProvider>(code);
}
