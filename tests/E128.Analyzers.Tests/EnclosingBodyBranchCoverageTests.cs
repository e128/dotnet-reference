using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EnclosingBodyBranchCoverageTests
{
    private static BlockSyntax? Find(string members)
    {
        var tree = CSharpSyntaxTree.ParseText(
            "using System; class C { " + members + " }",
            cancellationToken: TestContext.Current.CancellationToken);
        var node = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .First(static name => string.Equals(name.Identifier.ValueText, "Probe", System.StringComparison.Ordinal));
        return EnclosingBody.Find(node);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("void M() { Probe(); }")]
    [InlineData("C() { Probe(); }")]
    [InlineData("void M() { void Local() { Probe(); } }")]
    [InlineData("int P { get { Probe(); return 0; } }")]
    [InlineData("int P { set { Probe(); } }")]
    [InlineData("void M() { Action a = () => { Probe(); }; }")]
    [InlineData("void M() { Action a = delegate { Probe(); }; }")]
    public void Find_ReturnsTheBlockOfTheNearestEnclosingBody(string members)
    {
        Assert.NotNull(Find(members));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("int M() => Probe();")]
    [InlineData("int P => Probe();")]
    [InlineData("int F = Probe();")]
    [InlineData("void M() { Func<int> a = () => Probe(); }")]
    [InlineData("void M() { int Local() => Probe(); }")]
    public void Find_ReturnsNullForExpressionBodiesAndMembersWithoutABlock(string members)
    {
        Assert.Null(Find(members));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Find_PrefersTheInnermostBody()
    {
        var block = Find("void M() { void Local() { Probe(); } }");
        _ = Assert.IsType<LocalFunctionStatementSyntax>(block?.Parent);
    }
}
