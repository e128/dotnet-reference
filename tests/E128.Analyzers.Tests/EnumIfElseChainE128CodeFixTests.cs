using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EnumIfElseChainE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<EnumIfElseChainAnalyzer, EnumIfElseChainCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task ThreeBranchChain_ConvertsToSwitch()
    {
        const string source = """
                              enum Color { Red, Green, Blue }

                              class C
                              {
                                  int M(Color c)
                                  {
                                      {|E128048:if (c == Color.Red)
                                      {
                                          return 1;
                                      }
                                      else if (c == Color.Green)
                                      {
                                          return 2;
                                      }
                                      else if (c == Color.Blue)
                                      {
                                          return 3;
                                      }|}
                                      return 0;
                                  }
                              }
                              """;

        const string fixedCode = """
                                 enum Color { Red, Green, Blue }

                                 class C
                                 {
                                     int M(Color c)
                                     {
                                         switch (c)
                                         {
                                             case Color.Red:
                                                 return 1;
                                             case Color.Green:
                                                 return 2;
                                             case Color.Blue:
                                                 return 3;
                                         }
                                         return 0;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReversedComparisonChain_UsesTheVariableAsSwitchSubject()
    {
        const string source = """
                              enum Color { Red, Green, Blue }

                              class C
                              {
                                  int M(Color c)
                                  {
                                      {|E128048:if (Color.Red == c)
                                      {
                                          return 1;
                                      }
                                      else if (Color.Green == c)
                                      {
                                          return 2;
                                      }
                                      else if (Color.Blue == c)
                                      {
                                          return 3;
                                      }|}
                                      return 0;
                                  }
                              }
                              """;

        const string fixedCode = """
                                 enum Color { Red, Green, Blue }

                                 class C
                                 {
                                     int M(Color c)
                                     {
                                         switch (c)
                                         {
                                             case Color.Red:
                                                 return 1;
                                             case Color.Green:
                                                 return 2;
                                             case Color.Blue:
                                                 return 3;
                                         }
                                         return 0;
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
