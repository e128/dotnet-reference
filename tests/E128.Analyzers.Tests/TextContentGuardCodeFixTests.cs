using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TextContentGuardCodeFixTests
{
    private const string AngleSharpStubs = """
                                           namespace AngleSharp.Dom
                                           {
                                               public interface INode { string TextContent { get; } }
                                               public interface IElement : INode { bool IsVisible { get; } }
                                           }
                                           """;

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<TextContentGuardAnalyzer, TextContentGuardCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsLengthGuardBeforeMatch()
    {
        return VerifyFixAsync(
            $$"""
              {{AngleSharpStubs}}
              class C
              {
                  bool M(AngleSharp.Dom.IElement e)
                  {
                      return {|E128077:e.TextContent.Contains("widget")|};
                  }
              }
              """,
            $$"""
              {{AngleSharpStubs}}
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
              {{AngleSharpStubs}}
              class C
              {
                  bool M(AngleSharp.Dom.IElement e)
                  {
                      return e.IsVisible && {|E128077:e.TextContent.Contains("widget")|};
                  }
              }
              """,
            $$"""
              {{AngleSharpStubs}}
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
              {{AngleSharpStubs}}
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
              {{AngleSharpStubs}}
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
