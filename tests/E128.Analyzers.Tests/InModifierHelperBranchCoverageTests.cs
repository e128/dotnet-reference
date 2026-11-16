using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InModifierHelperBranchCoverageTests
{
    private static SyntaxTokenListProbe Modifiers(string declaration)
    {
        var member = SyntaxFactory.ParseMemberDeclaration(declaration)!;
        return new SyntaxTokenListProbe(member.Modifiers);
    }

    private readonly record struct SyntaxTokenListProbe(Microsoft.CodeAnalysis.SyntaxTokenList List);

    private static ParameterSyntax Parameter(string text)
    {
        return SyntaxFactory.ParseParameterList("(" + text + ")").Parameters[0];
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("public int F;", SyntaxKind.StaticKeyword, "public static")]
    [InlineData("private readonly int F;", SyntaxKind.StaticKeyword, "private static readonly")]
    [InlineData("private int F;", SyntaxKind.ReadOnlyKeyword, "private readonly")]
    [InlineData("static int F;", SyntaxKind.PublicKeyword, "public static")]
    [InlineData("int F;", SyntaxKind.StaticKeyword, "static")]
    [InlineData("private static int F;", SyntaxKind.StaticKeyword, "private static")]
    [InlineData("private int F;", SyntaxKind.ConstKeyword, "private const")]
    [InlineData("const int F = 1;", SyntaxKind.StaticKeyword, "static const")]
    public void AddModifier_PlacesTheTokenInLanguageReferenceOrder(string declaration, SyntaxKind kind, string expected)
    {
        var result = InModifierHelper.AddModifier(Modifiers(declaration).List, kind);
        Assert.Equal(expected, string.Join(' ', result.Select(static token => token.Text)));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("in int value", true)]
    [InlineData("ref int value", false)]
    [InlineData("int value", false)]
    [InlineData("this in int value", true)]
    public void HasInModifier_FindsTheInKeyword(string text, bool expected)
    {
        Assert.Equal(expected, InModifierHelper.HasInModifier(Parameter(text)));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("in int value", "int value")]
    [InlineData("this in int value", "this int value")]
    public void RemoveInModifier_DropsOnlyTheInKeyword(string text, string expected)
    {
        Assert.Equal(expected, InModifierHelper.RemoveInModifier(Parameter(text)).ToString());
    }

    [Fact]
    [Trait("Category", "CI")]
    public void RemoveInModifier_KeepsTheLeadingTriviaOfTheTypeAfterTheLastModifier()
    {
        var parameter = SyntaxFactory.ParseParameterList("(\n  in int value)").Parameters[0];
        Assert.Equal("  int value", InModifierHelper.RemoveInModifier(parameter).ToFullString());
    }
}
