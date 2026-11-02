using System;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UsingDirectivesTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void AddIfMissing_KeepsTheFileHeaderAboveTheNewDirective()
    {
        // A file header is leading trivia of the first using directive. A new directive that sorts
        // first must carry that trivia, not move above it.
        var root = SyntaxFactory.ParseCompilationUnit(
            "// Copyright header\nusing System.Text;\n\nclass C { }\n");

        var result = (CompilationUnitSyntax)UsingDirectives.AddIfMissing(
            root,
            "Microsoft.Extensions.Logging");

        var text = result.ToFullString();
        Assert.StartsWith("// Copyright header", text, StringComparison.Ordinal);
        Assert.True(
            text.IndexOf("using Microsoft.Extensions.Logging;", StringComparison.Ordinal)
            < text.IndexOf("using System.Text;", StringComparison.Ordinal),
            text);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void AddIfMissing_DoesNotDuplicateAnExistingDirective()
    {
        var root = SyntaxFactory.ParseCompilationUnit(
            "using System.Text;\n\nclass C { }\n");

        var result = (CompilationUnitSyntax)UsingDirectives.AddIfMissing(root, "System.Text");

        Assert.Single(result.Usings);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void AddIfMissing_KeepsTheFileHeaderOfAFileWithNoUsingsAboveTheNewDirective()
    {
        // With no using directives the header is leading trivia of the first member. The new
        // directive must carry that trivia, not land above the header.
        var root = SyntaxFactory.ParseCompilationUnit(
            "// Copyright header\nclass C { }\n");

        var result = (CompilationUnitSyntax)UsingDirectives.AddIfMissing(root, "System.Text");

        var text = result.ToFullString();
        Assert.StartsWith("// Copyright header", text, StringComparison.Ordinal);
        Assert.True(
            text.IndexOf("using System.Text;", StringComparison.Ordinal)
            < text.IndexOf("class C { }", StringComparison.Ordinal),
            text);
    }
}
