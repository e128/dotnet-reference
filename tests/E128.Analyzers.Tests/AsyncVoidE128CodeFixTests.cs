using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncVoidE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<AsyncVoidAnalyzer, AsyncVoidCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncVoid_CodeFix_ChangesVoidToTask()
    {
        const string source = """
                              using System.Threading.Tasks;
                              class C
                              {
                                  async void {|E128007:DoWork|}()
                                  {
                                      await Task.Delay(1);
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                     async Task DoWork()
                                     {
                                         await Task.Delay(1);
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncVoid_CodeFix_AddsUsingWhenMissing()
    {
        const string source = """
                              class C
                              {
                                  async void {|E128007:DoWork|}()
                                  {
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Threading.Tasks;

                                 class C
                                 {
                                     async Task DoWork()
                                     {
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncVoid_NoFix_WhenMethodOverridesABaseMethod()
    {
        // The base declares void. Changing only the override gives CS0508.
        return VerifyNoFixAsync("""
                                using System.Threading.Tasks;

                                class B
                                {
                                    protected virtual void OnLoad()
                                    {
                                    }
                                }

                                class C : B
                                {
                                    protected override async void {|E128007:OnLoad|}()
                                    {
                                        await Task.Delay(1);
                                    }
                                }
                                """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncVoid_NoFix_WhenMethodImplementsAnInterface()
    {
        // The interface declares void. Changing only the implementation gives CS0738.
        return VerifyNoFixAsync("""
                                using System.Threading.Tasks;

                                interface IFoo
                                {
                                    void Run();
                                }

                                class C : IFoo
                                {
                                    async void IFoo.{|E128007:Run|}()
                                    {
                                        await Task.Delay(1);
                                    }
                                }
                                """);
    }

    private static Task VerifyNoFixAsync(string code) =>
        CodeFixTestRunner.NoFixAsync<AsyncVoidAnalyzer, AsyncVoidCodeFixProvider>(code);
}
