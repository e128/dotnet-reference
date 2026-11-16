using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ExclusiveBranchesBranchCoverageTests
{
    private static InvocationExpressionSyntax[] Calls(string body)
    {
        var tree = CSharpSyntaxTree.ParseText(
            "class C { void M(int k, bool b) { " + body + " } void A() { } void B() { } void D() { } }",
            cancellationToken: TestContext.Current.CancellationToken);
        return [.. tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<InvocationExpressionSyntax>()];
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("if (b) { A(); } else { B(); }", true)]
    [InlineData("if (b) A(); else B();", true)]
    [InlineData("if (b) { A(); } else if (k > 1) { B(); }", true)]
    [InlineData("if (b) { A(); B(); }", false)]
    [InlineData("if (b) { } else { A(); B(); }", false)]
    [InlineData("A(); if (b) { B(); }", false)]
    [InlineData("if (b) { A(); } B();", false)]
    [InlineData("switch (k) { case 1: A(); break; case 2: B(); break; }", true)]
    [InlineData("switch (k) { case 1: A(); B(); break; }", false)]
    [InlineData("switch (k) { case 1: A(); break; } B();", false)]
    [InlineData("A(); switch (k) { case 1: B(); break; }", false)]
    [InlineData("switch (k) { case 1: case 2: A(); break; case 3: B(); break; }", true)]
    public void Are_ComparesTheBranchesOfTheSharedStatement(string body, bool expected)
    {
        var calls = Calls(body);
        Assert.Equal(expected, ExclusiveBranches.Are(calls[0], calls[1]));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Are_SeesDifferentSectionsOfAnOuterSwitchAroundAnInnerIf()
    {
        var calls = Calls("switch (k) { case 1: if (b) { A(); } break; case 2: if (b) { B(); } break; }");
        Assert.True(ExclusiveBranches.Are(calls[0], calls[1]));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Are_IsFalseForTheSameNodeInOneBranch()
    {
        var calls = Calls("if (b) { A(); } else { B(); }");
        Assert.False(ExclusiveBranches.Are(calls[0], calls[0]));
    }
}
