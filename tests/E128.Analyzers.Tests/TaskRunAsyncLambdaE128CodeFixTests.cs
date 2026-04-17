using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskRunAsyncLambdaE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<TaskRunAsyncLambdaAnalyzer, TaskRunAsyncLambdaCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

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

    private static Task VerifyNoFixAsync(string code)
    {
        return new CSharpCodeFixTest<TaskRunAsyncLambdaAnalyzer, TaskRunAsyncLambdaCodeFixProvider, DefaultVerifier>
        {
            TestCode = code,
            FixedCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 0
        }.RunAsync();
    }
}
