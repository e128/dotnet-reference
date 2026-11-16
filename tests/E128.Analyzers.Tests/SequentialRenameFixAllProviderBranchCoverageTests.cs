using System;
using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SequentialRenameFixAllProviderBranchCoverageTests
{
    private const string Source = "class C { int a; int b; }";

    private static (Solution Solution, DocumentId Id, ProjectId ProjectId) CreateSolution(AdhocWorkspace workspace)
    {
        var project = workspace.AddProject("P", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = project.AddDocument("Test.cs", Source);
        return (document.Project.Solution, document.Id, project.Id);
    }

    private static async Task<string> RenameAsync(Solution solution, DocumentId id, params SequentialRenameFixAllProvider.RenameInfo[] renames)
    {
        var result = await SequentialRenameFixAllProvider.ApplyRenamesSequentiallyAsync(
            solution,
            [.. renames],
            CancellationToken.None);
        var text = await result.GetDocument(id)!.GetTextAsync(TestContext.Current.CancellationToken);
        return text.ToString();
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task ApplyRenames_ChangesNothingForAnEmptyList()
    {
        using var workspace = new AdhocWorkspace();
        var (solution, id, _) = CreateSolution(workspace);
        Assert.Equal(Source, await RenameAsync(solution, id));
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task ApplyRenames_SkipsADocumentThatIsNotInTheSolution()
    {
        using var workspace = new AdhocWorkspace();
        var (solution, id, projectId) = CreateSolution(workspace);
        var missing = DocumentId.CreateNewId(projectId);

        var text = await RenameAsync(solution, id, new SequentialRenameFixAllProvider.RenameInfo(missing, new TextSpan(0, 1), "a", "alpha"));

        Assert.Equal(Source, text);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task ApplyRenames_SkipsASymbolWhoseNameChanged()
    {
        using var workspace = new AdhocWorkspace();
        var (solution, id, _) = CreateSolution(workspace);
        var start = Source.IndexOf("int a;", StringComparison.Ordinal) + 4;

        var text = await RenameAsync(solution, id, new SequentialRenameFixAllProvider.RenameInfo(id, new TextSpan(start, 1), "other", "alpha"));

        Assert.Equal(Source, text);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task ApplyRenames_SkipsASpanThatHoldsNoSymbol()
    {
        using var workspace = new AdhocWorkspace();
        var (solution, id, _) = CreateSolution(workspace);
        var start = Source.IndexOf("int a;", StringComparison.Ordinal) + 5;

        var text = await RenameAsync(solution, id, new SequentialRenameFixAllProvider.RenameInfo(id, new TextSpan(start, 1), "a", "alpha"));

        Assert.Equal(Source, text);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task ApplyRenames_AppliesEveryValidRenameInOrder()
    {
        using var workspace = new AdhocWorkspace();
        var (solution, id, _) = CreateSolution(workspace);
        var aStart = Source.IndexOf("int a;", StringComparison.Ordinal) + 4;
        var bStart = Source.IndexOf("int b;", StringComparison.Ordinal) + 4;

        var text = await RenameAsync(
            solution,
            id,
            new SequentialRenameFixAllProvider.RenameInfo(id, new TextSpan(bStart, 1), "b", "beta"),
            new SequentialRenameFixAllProvider.RenameInfo(id, new TextSpan(aStart, 1), "a", "alpha"));

        Assert.Equal("class C { int alpha; int beta; }", text);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void RenameInfo_ComparesAllFourParts()
    {
        var id = DocumentId.CreateNewId(ProjectId.CreateNewId());
        var other = DocumentId.CreateNewId(ProjectId.CreateNewId());
        var baseline = new SequentialRenameFixAllProvider.RenameInfo(id, new TextSpan(1, 2), "a", "b");

        Assert.True(baseline == new SequentialRenameFixAllProvider.RenameInfo(id, new TextSpan(1, 2), "a", "b"));
        Assert.True(baseline != new SequentialRenameFixAllProvider.RenameInfo(other, new TextSpan(1, 2), "a", "b"));
        Assert.True(baseline != new SequentialRenameFixAllProvider.RenameInfo(id, new TextSpan(1, 3), "a", "b"));
        Assert.True(baseline != new SequentialRenameFixAllProvider.RenameInfo(id, new TextSpan(1, 2), "x", "b"));
        Assert.True(baseline != new SequentialRenameFixAllProvider.RenameInfo(id, new TextSpan(1, 2), "a", "y"));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void RenameInfo_ObjectEqualityAndHashFollowTheValue()
    {
        var id = DocumentId.CreateNewId(ProjectId.CreateNewId());
        var first = new SequentialRenameFixAllProvider.RenameInfo(id, new TextSpan(1, 2), "a", "b");
        object boxed = new SequentialRenameFixAllProvider.RenameInfo(id, new TextSpan(1, 2), "a", "b");

        Assert.True(first.Equals(boxed));
        Assert.False(first.Equals("text"));
        Assert.False(first.Equals(null));
        Assert.Equal(first.GetHashCode(), boxed.GetHashCode());
        Assert.Equal("a to b", first.ToString());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixAll_RenamesEveryViolationInOneDocument()
    {
        return new CSharpCodeFixTest<FakeNamingViolationAnalyzer, NamingStyleCodeFixProvider, DefaultVerifier>
        {
            TestCode = """
                class C
                {
                    private int {|IDE1006:first|} = 0;
                    private int {|IDE1006:second|} = 0;
                    private int {|IDE1006:third|} = 0;

                    public int Sum() => first + second + third;
                }
                """,
            FixedCode = """
                class C
                {
                    private int _first = 0;
                    private int _second = 0;
                    private int _third = 0;

                    public int Sum() => _first + _second + _third;
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        }.RunAsync(TestContext.Current.CancellationToken);
    }
}
