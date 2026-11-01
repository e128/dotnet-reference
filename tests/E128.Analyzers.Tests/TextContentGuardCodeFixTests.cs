using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TextContentGuardCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<TextContentGuardAnalyzer, TextContentGuardCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsLengthGuardBeforeMatch()
    {
        return VerifyFixAsync(
            $$"""
              {{TestStubs.AngleSharpDom}}
              class C
              {
                  bool M(AngleSharp.Dom.IElement e)
                  {
                      return {|E128077:e.TextContent.Contains("widget")|};
                  }
              }
              """,
            $$"""
              {{TestStubs.AngleSharpDom}}
              class C
              {
                  bool M(AngleSharp.Dom.IElement e)
                  {
                      return e.TextContent.Length < 101 && e.TextContent.Contains("widget");
                  }
              }
              """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ExtendsExistingAndChain()
    {
        return VerifyFixAsync(
            $$"""
              {{TestStubs.AngleSharpDom}}
              class C
              {
                  bool M(AngleSharp.Dom.IElement e)
                  {
                      return e.IsVisible && {|E128077:e.TextContent.Contains("widget")|};
                  }
              }
              """,
            $$"""
              {{TestStubs.AngleSharpDom}}
              class C
              {
                  bool M(AngleSharp.Dom.IElement e)
                  {
                      return e.TextContent.Length < 101 && e.IsVisible && e.TextContent.Contains("widget");
                  }
              }
              """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ParenthesizesUnderNegation()
    {
        return VerifyFixAsync(
            $$"""
              {{TestStubs.AngleSharpDom}}
              class C
              {
                  void M(AngleSharp.Dom.IElement e)
                  {
                      if (!{|E128077:e.TextContent.Contains("widget")|})
                      {
                          return;
                      }
                  }
              }
              """,
            $$"""
              {{TestStubs.AngleSharpDom}}
              class C
              {
                  void M(AngleSharp.Dom.IElement e)
                  {
                      if (!(e.TextContent.Length < 101 && e.TextContent.Contains("widget")))
                      {
                          return;
                      }
                  }
              }
              """);
    }
}
