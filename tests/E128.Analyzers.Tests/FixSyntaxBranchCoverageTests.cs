using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FixSyntaxBranchCoverageTests
{
    private static CompilationUnitSyntax Parse(string code)
    {
        return SyntaxFactory.ParseCompilationUnit(code);
    }

    private static T First<T>(SyntaxNode root)
        where T : SyntaxNode
    {
        return root.DescendantNodes().OfType<T>().First();
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("static class C { void M() { Probe(); } }", true)]
    [InlineData("class C { static void M() { Probe(); } }", true)]
    [InlineData("class C { static int P { get { Probe(); return 0; } } }", true)]
    [InlineData("class C { static int F = Probe(); }", true)]
    [InlineData("class C { void M() { Probe(); } }", false)]
    [InlineData("class C { int P { get { Probe(); return 0; } } }", false)]
    [InlineData("class C { int F = Probe(); }", false)]
    [InlineData("struct S { void M() { Probe(); } }", false)]
    public void IsInStaticMember_ChecksEveryEnclosingDeclaration(string code, bool expected)
    {
        var node = First<InvocationExpressionSyntax>(Parse(code));
        Assert.Equal(expected, FixSyntax.IsInStaticMember(node));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("() => new Widget()", true)]
    [InlineData("x => new Widget()", true)]
    [InlineData("() => new Widget { Name = \"a\" }", true)]
    [InlineData("() => new Widget(1)", false)]
    [InlineData("() => Widget.Create()", false)]
    [InlineData("() => { return new Widget(); }", false)]
    [InlineData("new Widget()", false)]
    public void IsParameterlessCreation_AcceptsOnlyExpressionLambdasThatCreateAnObject(string text, bool expected)
    {
        Assert.Equal(expected, FixSyntax.IsParameterlessCreation(SyntaxFactory.ParseExpression(text)));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void MemberIndentation_ReadsTheLeadingWhitespace()
    {
        var member = First<MethodDeclarationSyntax>(Parse("class C\n{\n        void M() { }\n}\n"));
        Assert.Equal("        ", FixSyntax.MemberIndentation(member));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void MemberIndentation_FallsBackToFourSpacesWithoutLeadingWhitespace()
    {
        var member = First<MethodDeclarationSyntax>(Parse("class C {void M() { }}"));
        Assert.Equal("    ", FixSyntax.MemberIndentation(member));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void LineBreak_ReturnsTheTrailingEndOfLine()
    {
        var member = First<MethodDeclarationSyntax>(Parse("class C\r\n{\r\n    void M() { }\r\n}\r\n"));
        Assert.Equal("\r\n", FixSyntax.LineBreak(member));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void LineBreak_DefaultsToANewlineWhenNoneFollows()
    {
        var member = First<MethodDeclarationSyntax>(Parse("class C { void M() { } }"));
        Assert.Equal("\n", FixSyntax.LineBreak(member));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void InsertFieldAsFirstMember_AddsTheFieldToAnEmptyType()
    {
        var type = First<ClassDeclarationSyntax>(Parse("class C { }"));
        var field = SyntaxFactory.ParseMemberDeclaration("private int _value;")!;

        var result = FixSyntax.InsertFieldAsFirstMember(type, field);

        var member = Assert.Single(result.Members);
        _ = Assert.IsType<FieldDeclarationSyntax>(member);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void InsertFieldAsFirstMember_PutsTheFieldBeforeTheFirstMemberWithTheSameIndent()
    {
        var type = First<ClassDeclarationSyntax>(Parse("class C\n{\n    void M() { }\n}\n"));
        var field = SyntaxFactory.ParseMemberDeclaration("private int _value;")!;

        var result = FixSyntax.InsertFieldAsFirstMember(type, field);

        Assert.Equal(2, result.Members.Count);
        _ = Assert.IsType<FieldDeclarationSyntax>(result.Members[0]);
        Assert.Equal("    ", result.Members[0].GetLeadingTrivia().ToFullString());
        Assert.Contains("\n\n", result.Members[1].GetLeadingTrivia().ToFullString(), System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void LastIndentation_ReturnsTheLastWhitespaceItem()
    {
        var trivia = SyntaxFactory.TriviaList(SyntaxFactory.Whitespace("  "), SyntaxFactory.Comment("// x"), SyntaxFactory.Whitespace("    "));
        Assert.Equal("    ", FixSyntax.LastIndentation(trivia));
        Assert.Empty(FixSyntax.LastIndentation(SyntaxFactory.TriviaList(SyntaxFactory.Comment("// x"))));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TrailingWhitespace_KeepsOnlyAFinalWhitespaceItem()
    {
        var withSpace = SyntaxFactory.TriviaList(SyntaxFactory.Comment("// x"), SyntaxFactory.Whitespace("  "));
        var withComment = SyntaxFactory.TriviaList(SyntaxFactory.Whitespace("  "), SyntaxFactory.Comment("// x"));

        Assert.Equal("  ", FixSyntax.TrailingWhitespace(withSpace).ToFullString());
        Assert.Empty(FixSyntax.TrailingWhitespace(withComment));
        Assert.Empty(FixSyntax.TrailingWhitespace(SyntaxFactory.TriviaList()));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("items", "items")]
    [InlineData("this.items", "this.items")]
    [InlineData("Make()", "Make()")]
    [InlineData("items[0]", "items[0]")]
    [InlineData("this", "this")]
    [InlineData("(a + b)", "(a + b)")]
    [InlineData("a + b", "(a + b)")]
    [InlineData("x ?? y", "(x ?? y)")]
    public void ParenthesizeIfNeeded_WrapsOnlyNonPrimaryExpressions(string text, string expected)
    {
        Assert.Equal(expected, FixSyntax.ParenthesizeIfNeeded(SyntaxFactory.ParseExpression(text)).ToString());
    }

    [Fact]
    [Trait("Category", "CI")]
    public void IndexInto_ParenthesizesAnOperandThatNeedsIt()
    {
        var result = FixSyntax.IndexInto(SyntaxFactory.ParseExpression("a ?? b"), SyntaxFactory.ParseExpression(" i "));
        Assert.Equal("(a ?? b)[i]", result.ToString());
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Settings.Current", "x", "", "_current")]
    [InlineData("client", "x", "", "_client")]
    [InlineData("Client", "x", "Suffix", "_clientSuffix")]
    [InlineData("a.b.C1", "x", "", "_c1")]
    [InlineData("<>", "Fallback", "", "_fallback")]
    [InlineData("a.()", "Other", "", "_other")]
    public void BuildFieldName_LowersTheLastNameOrUsesTheFallback(string operand, string fallback, string suffix, string expected)
    {
        Assert.Equal(expected, FixSyntax.BuildFieldName(operand, fallback, suffix));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("class C { int _a, _b; }", "_b", false, false, true)]
    [InlineData("class C { int P { get; } }", "P", true, false, true)]
    [InlineData("class C { int P { get; } }", "P", false, true, false)]
    [InlineData("class C { void P() { } }", "P", false, true, true)]
    [InlineData("class C { void P() { } }", "P", true, false, false)]
    [InlineData("class C { event System.Action P; }", "P", true, true, false)]
    [InlineData("class C { int _a; }", "_z", true, true, false)]
    public void HasMemberNamed_ReadsFieldsAndOptionalMembers(
        string code,
        string name,
        bool includeProperties,
        bool includeMethods,
        bool expected)
    {
        var type = First<TypeDeclarationSyntax>(Parse(code));
        Assert.Equal(expected, FixSyntax.HasMemberNamed(type, name, includeProperties, includeMethods));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("class C { void M() { Probe(await x); } }", "class C { async Task M() { Probe(await x); } }")]
    [InlineData("class C { int M() { Probe(await x); } }", "class C { async Task<int> M() { Probe(await x); } }")]
    [InlineData("class C { Task M() { Probe(await x); } }", "class C { async Task M() { Probe(await x); } }")]
    [InlineData("class C { ValueTask<int> M() { Probe(await x); } }", "class C { async ValueTask<int> M() { Probe(await x); } }")]
    [InlineData("class C { async void M() { Probe(await x); } }", "class C { async void M() { Probe(await x); } }")]
    [InlineData("class C { int P { get { return Probe(await x); } } }", "class C { int P { get { return Probe(await x); } } }")]
    public void PromoteContainingMethod_AddsAsyncAndTheTaskReturnType(string code, string expected)
    {
        var root = Parse(code);
        var awaitExpression = First<AwaitExpressionSyntax>(root);

        var result = FixSyntax.PromoteContainingMethod(root, awaitExpression);

        Assert.Equal(expected, result.ToFullString().Replace("  ", " ", System.StringComparison.Ordinal));
    }
}
