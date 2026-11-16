using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UsingDirectivesBranchCoverageTests
{
    private static string Add(string code, string ns)
    {
        var root = SyntaxFactory.ParseCompilationUnit(code);
        return UsingDirectives.AddIfMissing(root, ns).NormalizeWhitespace(eol: "\n").ToFullString();
    }

    [Fact]
    [Trait("Category", "CI")]
    public void AddIfMissing_ReturnsANodeThatIsNotACompilationUnitUnchanged()
    {
        var node = SyntaxFactory.ParseStatement("return;");
        Assert.Same(node, UsingDirectives.AddIfMissing(node, "System.IO"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void AddIfMissing_KeepsAFileThatHasTheDirective()
    {
        const string code = "using System.IO;\nclass C { }\n";
        Assert.Equal(SyntaxFactory.ParseCompilationUnit(code).NormalizeWhitespace(eol: "\n").ToFullString(), Add(code, "System.IO"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void AddIfMissing_InsertsAfterTheLastDirectiveThatSortsBefore()
    {
        Assert.Equal(
            "using System;\nusing System.IO;\nusing System.Text;\n\nclass C\n{\n}",
            Add("using System;\nusing System.Text;\nclass C { }\n", "System.IO"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void AddIfMissing_InsertsAtTheEndWhenEveryDirectiveSortsBefore()
    {
        Assert.Equal(
            "using System;\nusing System.IO;\n\nclass C\n{\n}",
            Add("using System;\nclass C { }\n", "System.IO"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void AddIfMissing_MovesTheFileHeaderFromTheFirstDirective()
    {
        var result = Add("// header\nusing System.Text;\nclass C { }\n", "System.IO");
        Assert.StartsWith("// header\nusing System.IO;\nusing System.Text;", result, System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void AddIfMissing_MovesTheFileHeaderFromTheFirstMemberWhenThereAreNoDirectives()
    {
        var result = Add("// header\nclass C { }\n", "System.IO");
        Assert.StartsWith("// header\nusing System.IO;", result, System.StringComparison.Ordinal);
        Assert.Contains("class C", result, System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void AddIfMissing_WorksForAnEmptyFile()
    {
        Assert.StartsWith("using System.IO;", Add(string.Empty, "System.IO"), System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void AddIfMissing_WorksForAFileWithoutLeadingTrivia()
    {
        Assert.StartsWith("using System.IO;", Add("class C { }", "System.IO"), System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async System.Threading.Tasks.Task EnsureAddedAsync_AddsTheDirectiveToTheDocument()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("P", LanguageNames.CSharp).AddDocument("Test.cs", "class C { }");

        var result = await UsingDirectives.EnsureAddedAsync(document, "System.IO", TestContext.Current.CancellationToken);

        var root = await result.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(root);
        Assert.StartsWith("using System.IO;", root.NormalizeWhitespace().ToFullString(), System.StringComparison.Ordinal);
    }
}
