using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LoopContextBranchCoverageMoreTests
{
    private const string Prelude = """
        using System;

        class C
        {
            int[] items = new int[4];
            (int, int)[] pairs = new (int, int)[4];
            int Use(int value) { return value; }

        """;

    private static SyntaxNode Parse(string members)
    {
        var tree = CSharpSyntaxTree.ParseText(
            Prelude + members + "\n}\n",
            cancellationToken: TestContext.Current.CancellationToken);
        return tree.GetRoot(TestContext.Current.CancellationToken);
    }

    private static T Find<T>(SyntaxNode root, string text)
        where T : SyntaxNode
    {
        return root.DescendantNodes().OfType<T>().First(n => string.Equals(n.ToString(), text, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void FindEnclosingLoop_IsNullForANodeOutsideEveryLoop()
    {
        var root = Parse("void M() { Use(1); }");
        Assert.Null(LoopContext.FindEnclosingLoop(Find<InvocationExpressionSyntax>(root, "Use(1)")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsWithinAnyLoop_IsFalseForANodeInAFieldInitializer()
    {
        var root = Parse("int field = Use(1);");
        Assert.False(LoopContext.IsWithinAnyLoop(Find<InvocationExpressionSyntax>(root, "Use(1)")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsInsideLoopBody_IsFalseForANodeInAnExpressionBodiedProperty()
    {
        var root = Parse("int P => Use(1);");
        Assert.False(LoopContext.IsInsideLoopBody(Find<InvocationExpressionSyntax>(root, "Use(1)")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsInsideLoopBody_IsFalseForANodeInAForLoopWithoutACondition()
    {
        var root = Parse("void M() { for (var i = Use(1); ; i++) { } }");
        Assert.False(LoopContext.IsInsideLoopBody(Find<InvocationExpressionSyntax>(root, "Use(1)")));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsInsideLoopBody_IsFalseForTheTypeOfAForeachVariable()
    {
        var root = Parse("void M() { foreach (int item in items) { } }");
        var type = root.DescendantNodes().OfType<ForEachStatementSyntax>().Single().Type;
        Assert.False(LoopContext.IsInsideLoopBody(type));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IsInsideLoopBody_CountsTheDeconstructedForeachVariable()
    {
        var root = Parse("void M() { foreach (var (a, b) in pairs) { } }");
        var variable = root.DescendantNodes().OfType<ForEachVariableStatementSyntax>().Single().Variable;
        Assert.True(LoopContext.IsInsideLoopBody(variable));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void FindEnclosingLoopBody_IsNullForTheDeconstructedForeachVariable()
    {
        var root = Parse("void M() { foreach (var (a, b) in pairs) { } }");
        var variable = root.DescendantNodes().OfType<ForEachVariableStatementSyntax>().Single().Variable;
        Assert.Null(LoopContext.FindEnclosingLoopBody(variable));
    }
}
