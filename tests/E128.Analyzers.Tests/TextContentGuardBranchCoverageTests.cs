using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the guard shapes that decide whether a TextContent match needs a length check.
/// </summary>
public sealed class TextContentGuardBranchCoverageTests
{
    private static Task VerifyAsync(string members, AnalyzerTestOptions? options = null)
    {
        var source = $$"""
                       using System;
                       {{TestStubs.AngleSharpDom}}
                       class C
                       {
                           bool flag;
                           int limit = 500;

                       {{members}}
                       }
                       """;
        return AnalyzerTestRunner.VerifyAsync<TextContentGuardAnalyzer>(source, expected: null, options);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("e.TextContent.Length <= 500 && e.TextContent.Contains(\"x\")")]
    [InlineData("500 > e.TextContent.Length && e.TextContent.Contains(\"x\")")]
    [InlineData("500 >= e.TextContent.Length && e.TextContent.Contains(\"x\")")]
    [InlineData("e.TextContent.Contains(\"x\") && 100 > e.TextContent.Length")]
    [InlineData("flag && e.TextContent.Length < 500 && e.TextContent.Contains(\"x\")")]
    [InlineData("e.TextContent.Length < 500 && flag && e.TextContent.EndsWith(\"x\")")]
    public Task Silent_WhenALengthGuardAccompaniesTheMatch(string condition)
    {
        return VerifyAsync($$"""
                                 bool M(AngleSharp.Dom.IElement e)
                                 {
                                     return {{condition}};
                                 }
                             """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("e.TextContent.Length > 50 && {|E128077:e.TextContent.Contains(\"x\")|}")]
    [InlineData("e.TextContent.Length > 200 && {|E128077:e.TextContent.Contains(\"x\")|}")]
    [InlineData("200 < e.TextContent.Length && {|E128077:e.TextContent.Contains(\"x\")|}")]
    [InlineData("50 < e.TextContent.Length && {|E128077:e.TextContent.Equals(\"x\")|}")]
    [InlineData("e.TextContent.Length < limit && {|E128077:e.TextContent.Contains(\"x\")|}")]
    [InlineData("e.TextContent.Length < 'a' && {|E128077:e.TextContent.Contains(\"x\")|}")]
    [InlineData("e.TextContent.Length < 5000000000L && {|E128077:e.TextContent.Contains(\"x\")|}")]
    [InlineData("e.TextContent.Length < 500 || {|E128077:e.TextContent.Contains(\"x\")|}")]
    [InlineData("flag && {|E128077:e.TextContent.Contains(\"x\")|}")]
    [InlineData("e.Other.Length < 500 && {|E128077:e.TextContent.Contains(\"x\")|}")]
    [InlineData("args.Length < 500 && {|E128077:e.TextContent.Contains(\"x\")|}")]
    [InlineData("other.TextContent.Length < 500 && {|E128077:e.TextContent.Contains(\"x\")|}")]
    public Task Reports_WhenNoUsableGuardAccompaniesTheMatch(string condition)
    {
        return VerifyAsync($$"""
                                 bool M(AngleSharp.Dom.IElement e, AngleSharp.Dom.IElement other, string[] args)
                                 {
                                     return {{condition}};
                                 }
                             """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnOrChainSplitsTheGuardFromTheMatch()
    {
        return VerifyAsync("""
                               bool M(AngleSharp.Dom.IElement e)
                               {
                                   return e.TextContent.Length < 500 && flag || {|E128077:e.TextContent.Contains("x")|};
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardChainIsFollowedByAnOr()
    {
        return VerifyAsync("""
                               bool M(AngleSharp.Dom.IElement e)
                               {
                                   return e.TextContent.Length < 500 && e.TextContent.Contains("x") || flag;
                               }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheMatchIsAMethodBodyWithoutLambda()
    {
        return VerifyAsync("""
                               bool M(AngleSharp.Dom.IElement e) => {|E128077:e.TextContent.Equals("x")|};
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsNotAMemberCall()
    {
        return VerifyAsync("""
                               bool Contains(string text) => text.Length > 0;

                               bool M(AngleSharp.Dom.IElement e) => Contains("x");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsNotAMemberAccess()
    {
        return VerifyAsync("""
                               bool M(string text) => text.Contains("x");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMemberIsNotTextContent()
    {
        return VerifyAsync("""
                               bool M(AngleSharp.Dom.IElement e) => e.ToString().Contains("x");

                               bool N(Uri uri) => uri.Host.Contains("x");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTextContentIsAField()
    {
        return VerifyAsync("""
                               private sealed class Widget
                               {
                                   public string TextContent = string.Empty;
                               }

                               bool M(Widget widget) => widget.TextContent.Contains("x");
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTextContentDoesNotBind()
    {
        return VerifyAsync(
            """
                bool M(Missing missing) => missing.TextContent.Contains("x");
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}
