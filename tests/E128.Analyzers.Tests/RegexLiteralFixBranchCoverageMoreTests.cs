using System;
using System.Linq;
using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RegexLiteralFixBranchCoverageMoreTests
{
    private static AttributeSyntax ParseAttribute(string attribute)
    {
        var tree = CSharpSyntaxTree.ParseText(
            "class C { " + attribute + " void M() { } }",
            cancellationToken: TestContext.Current.CancellationToken);

        return tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<AttributeSyntax>().Single();
    }

    private static async Task<string> RewriteAsync(string attribute, Func<string, string> rewrite)
    {
        var source = "class C { " + attribute + " void M() { } }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("Probe", LanguageNames.CSharp).AddDocument("Probe.cs", source);
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        var node = Assert.IsType<CompilationUnitSyntax>(root).DescendantNodes().OfType<AttributeSyntax>().Single();
        var result = await RegexLiteralFix.ApplyRewriteAsync(document, node, rewrite, TestContext.Current.CancellationToken);
        var text = await result.GetTextAsync(TestContext.Current.CancellationToken);

        return text.ToString();
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TryGetPattern_ReadsTheFirstPositionalStringLiteral()
    {
        Assert.True(RegexLiteralFix.TryGetPattern(ParseAttribute("""[GeneratedRegex("a+")]"""), out var pattern));
        Assert.Equal("a+", pattern);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("[GeneratedRegex]")]
    [InlineData("[GeneratedRegex()]")]
    [InlineData("""[GeneratedRegex(pattern: "a+")]""")]
    [InlineData("""[GeneratedRegex(Pattern = "a+")]""")]
    [InlineData("[GeneratedRegex(Pattern)]")]
    [InlineData("[GeneratedRegex(1)]")]
    public void TryGetPattern_IsFalseWhenNoPositionalStringLiteralLeads(string attribute)
    {
        Assert.False(RegexLiteralFix.TryGetPattern(ParseAttribute(attribute), out var pattern));
        Assert.Equal(string.Empty, pattern);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("[GeneratedRegex]")]
    [InlineData("[GeneratedRegex(Pattern)]")]
    public async Task ApplyRewriteAsync_KeepsTheDocumentWhenThereIsNoPattern(string attribute)
    {
        var rewritten = await RewriteAsync(attribute, static pattern => pattern + "x");

        Assert.Equal("class C { " + attribute + " void M() { } }", rewritten);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task ApplyRewriteAsync_KeepsTheDocumentWhenTheRewriteChangesNothing()
    {
        var rewritten = await RewriteAsync("""[GeneratedRegex("a+")]""", static pattern => pattern);

        Assert.Equal("""class C { [GeneratedRegex("a+")] void M() { } }""", rewritten);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task ApplyRewriteAsync_ReplacesARegularLiteral()
    {
        var rewritten = await RewriteAsync("""[GeneratedRegex("a+")]""", static pattern => pattern + "b");

        Assert.Equal("""class C { [GeneratedRegex("a+b")] void M() { } }""", rewritten);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task ApplyRewriteAsync_ReplacesAVerbatimLiteralAndDoublesItsQuotes()
    {
        var rewritten = await RewriteAsync("""[GeneratedRegex(@"a+")]""", static pattern => pattern + "\"");

        Assert.Equal("class C { [GeneratedRegex(@\"a+\"\"\")] void M() { } }", rewritten);
    }
}
