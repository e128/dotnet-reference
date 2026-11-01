using System.Threading.Tasks;
using E128.Analyzers.Style;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EmptyStringLiteralCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<EmptyStringLiteralAnalyzer, EmptyStringLiteralCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task VariableDeclaration_FixReplacesWithStringEmpty()
    {
        const string source = """
                              class C
                              {
                                  void M()
                                  {
                                      var x = {|E128002:""|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 class C
                                 {
                                     void M()
                                     {
                                         var x = string.Empty;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MultipleOccurrences_FixAllReplacesAll()
    {
        const string source = """
                              class C
                              {
                                  void M(string s)
                                  {
                                      var x = {|E128002:""|};
                                      var y = {|E128002:""|};
                                  }
                              }
                              """;

        const string fixedCode = """
                                 class C
                                 {
                                     void M(string s)
                                     {
                                         var x = string.Empty;
                                         var y = string.Empty;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
