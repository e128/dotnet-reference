using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SequentialRenameFixAllProviderTests
{
    [Fact]
    [Trait("Category", "CI")]
    public async Task StaleSpan_IsSkipped_AndValidRenameApplies()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("P", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = project.AddDocument("Test.cs", "class C { int a; int b; }");
        var solution = document.Project.Solution;
        var text = await document.GetTextAsync(TestContext.Current.CancellationToken);
        var source = text.ToString();
        var aStart = source.IndexOf("int a;", StringComparison.Ordinal) + 4;

        var renames = new List<SequentialRenameFixAllProvider.RenameInfo>
        {
            // This span lies past the end of the document, so the provider must skip it.
            new(document.Id, new TextSpan(text.Length + 10, 1), "b", "beta"),
            new(document.Id, new TextSpan(aStart, 1), "a", "alpha")
        };

        var result = await SequentialRenameFixAllProvider.ApplyRenamesSequentiallyAsync(
            solution,
            renames,
            CancellationToken.None);

        var renamedDocument = result.GetDocument(document.Id);
        Assert.NotNull(renamedDocument);
        var renamedText = (await renamedDocument.GetTextAsync(TestContext.Current.CancellationToken)).ToString();

        Assert.Contains("alpha", renamedText, StringComparison.Ordinal);
        Assert.DoesNotContain("beta", renamedText, StringComparison.Ordinal);
    }
}
