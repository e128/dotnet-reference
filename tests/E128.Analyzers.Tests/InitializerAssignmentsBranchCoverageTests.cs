using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InitializerAssignmentsBranchCoverageTests
{
    private static BaseObjectCreationExpressionSyntax Create(string text)
    {
        return (BaseObjectCreationExpressionSyntax)SyntaxFactory.ParseExpression(text);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("new Widget { Name = \"a\" }", "Name", true)]
    [InlineData("new Widget { Other = 1, Name = \"a\" }", "Name", true)]
    [InlineData("new Widget { Other = 1 }", "Name", false)]
    [InlineData("new Widget { [0] = 1 }", "Name", false)]
    [InlineData("new Widget { 1, 2 }", "Name", false)]
    [InlineData("new Widget()", "Name", false)]
    [InlineData("new Widget { }", "Name", false)]
    [InlineData("new() { Name = \"a\" }", "Name", true)]
    [InlineData("new Widget { name = \"a\" }", "Name", false)]
    public void Assigns_FindsTheNamedPropertyInTheInitializer(string text, string property, bool expected)
    {
        Assert.Equal(expected, InitializerAssignments.Assigns(Create(text), property));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Name", "Name")]
    [InlineData("a[0]", null)]
    [InlineData("(a)", null)]
    [InlineData("a()", null)]
    public void GetAssignedName_ReadsTheSimpleNameOnTheLeft(string text, string? expected)
    {
        Assert.Equal(expected, InitializerAssignments.GetAssignedName(SyntaxFactory.ParseExpression(text)));
    }
}
