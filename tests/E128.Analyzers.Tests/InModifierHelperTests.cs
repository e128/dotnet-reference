using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InModifierHelperTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void AddModifier_PicksTheLanguageReferencePosition()
    {
        Assert.Equal(
            "public static readonly",
            Text(Modify("static readonly", SyntaxKind.PublicKeyword)));
        Assert.Equal(
            "static async",
            Text(Modify("static", SyntaxKind.AsyncKeyword)));
        Assert.Equal(
            "virtual sealed readonly",
            Text(Modify("virtual sealed", SyntaxKind.ReadOnlyKeyword)));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void AddModifier_KeepsAnAlreadyPresentModifier()
    {
        var modifiers = List("public static");
        var result = InModifierHelper.AddModifier(modifiers, SyntaxKind.StaticKeyword);
        Assert.Equal(Text(modifiers), Text(result));
    }

    /// <summary>
    ///     Member modifiers such as public and static do not parse inside a parameter
    ///     list, so the tokens come from a field declaration instead.
    /// </summary>
    private static SyntaxTokenList List(string source)
    {
        var tree = SyntaxFactory.ParseSyntaxTree($"class C {{ {source} int x; }}");
        return tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>().First().Modifiers;
    }

    private static SyntaxTokenList Modify(string source, SyntaxKind kind)
    {
        return InModifierHelper.AddModifier(List(source), kind);
    }

    private static string Text(SyntaxTokenList modifiers)
    {
        return string.Join(' ', modifiers.Select(modifier => modifier.ValueText).ToList());
    }
}
