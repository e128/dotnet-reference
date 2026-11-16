using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class IndexedForLoopBranchCoverageTests
{
    private static ForStatementSyntax Loop(string text)
    {
        var tree = CSharpSyntaxTree.ParseText(
            "class C { void M() { " + text + " } }",
            cancellationToken: TestContext.Current.CancellationToken);
        return tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<ForStatementSyntax>().First();
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("for (var i = 0; i < items.Length; i++) { }", "i", "items")]
    [InlineData("for (int j = 0; j < this.items.Length; j++) { }", "j", "this.items")]
    [InlineData("for (var i = 0; i < Get().Length; i++) { }", "i", "Get()")]
    public void TryMatch_ReadsTheIndexVariableAndTheLengthOwner(string text, string variable, string owner)
    {
        var loop = Loop(text);

        Assert.True(IndexedForLoop.TryMatch(loop, out var name, out var boundOwner, out var body));
        Assert.Equal(variable, name);
        Assert.Equal(owner, boundOwner?.ToString());
        Assert.Same(loop.Statement, body);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("for (; ; ) { }")]
    [InlineData("for (var i = 0, j = 0; i < items.Length; i++) { }")]
    [InlineData("for (var i = 1; i < items.Length; i++) { }")]
    [InlineData("for (var i = start; i < items.Length; i++) { }")]
    [InlineData("for (int i; i < items.Length; i++) { }")]
    [InlineData("for (var i = 0; ; i++) { }")]
    [InlineData("for (var i = 0; i <= items.Length; i++) { }")]
    [InlineData("for (var i = 0; i < items.Count; i++) { }")]
    [InlineData("for (var i = 0; i < items.Length * 2; i++) { }")]
    [InlineData("for (var i = 0; 0 < items.Length; i++) { }")]
    [InlineData("for (var i = 0; i < items.Length; ) { }")]
    [InlineData("for (var i = 0; i < items.Length; i++, j++) { }")]
    [InlineData("for (var i = 0; i < items.Length; ++i) { }")]
    [InlineData("for (var i = 0; i < items.Length; i--) { }")]
    [InlineData("for (var i = 0; i < items.Length; i += 1) { }")]
    [InlineData("for (var i = 0; i < items.Length; items.Index++) { }")]
    [InlineData("for (var i = 0; j < items.Length; i++) { }")]
    [InlineData("for (var i = 0; i < items.Length; j++) { }")]
    [InlineData("for (i = 0; i < items.Length; i++) { }")]
    public void TryMatch_RejectsLoopsThatAreNotAZeroBasedLengthScan(string text)
    {
        var loop = Loop(text);

        Assert.False(IndexedForLoop.TryMatch(loop, out var name, out var boundOwner, out _));
        Assert.Empty(name);
        Assert.Null(boundOwner);
    }
}
