using System.Collections.Generic;
using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EnumIfElseChainCodeFixProviderBranchCoverageMoreTests
{
    /// <summary>
    ///     Applies the offered action directly and returns the resulting text. The test framework
    ///     rejects a fix that is offered and changes nothing.
    /// </summary>
    private static async Task<string> ApplyOfferedFixAsync(string source)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var references = await ReferenceAssemblies.Net.Net100.ResolveAsync(LanguageNames.CSharp, cancellationToken);

        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(references);
        var document = project.AddDocument("Test.cs", SourceText.From(source));

        var compilation = await document.Project.GetCompilationAsync(cancellationToken);
        Assert.NotNull(compilation);

        var diagnostics = await compilation
            .WithAnalyzers([new EnumIfElseChainAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
        var diagnostic = Assert.Single(diagnostics);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken);
        await new EnumIfElseChainCodeFixProvider().RegisterCodeFixesAsync(context);

        var operations = await Assert.Single(actions).GetOperationsAsync(cancellationToken);
        var changedDocument = Assert.Single(operations.OfType<ApplyChangesOperation>())
            .ChangedSolution.GetDocument(document.Id);
        Assert.NotNull(changedDocument);

        return (await changedDocument.GetTextAsync(cancellationToken)).ToString();
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Fix_LeavesTheChainWhenAComparisonHasNoEnumConstant()
    {
        const string Code = """
            enum Color { Red, Green, Blue }

            class C
            {
                int M(Color c, Color other)
                {
                    if (c == other)
                    {
                        return 1;
                    }
                    else if (c == Color.Green)
                    {
                        return 2;
                    }
                    else if (c == Color.Blue)
                    {
                        return 3;
                    }

                    return 0;
                }
            }
            """;

        Assert.Equal(Code, await ApplyOfferedFixAsync(Code));
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Fix_LeavesTheChainWhenAComparisonUsesALiteralZero()
    {
        const string Code = """
            enum Color { Red, Green, Blue }

            class C
            {
                int M(Color c)
                {
                    if (c == 0)
                    {
                        return 1;
                    }
                    else if (c == Color.Green)
                    {
                        return 2;
                    }
                    else if (c == Color.Blue)
                    {
                        return 3;
                    }

                    return 0;
                }
            }
            """;

        Assert.Equal(Code, await ApplyOfferedFixAsync(Code));
    }
}
