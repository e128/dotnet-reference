using System.Collections.Generic;
using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientResponseHeadersReadCodeFixProviderBranchCoverageMoreTests
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
            .WithAnalyzers([new HttpClientResponseHeadersReadAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
        var diagnostic = Assert.Single(diagnostics);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken);
        await new HttpClientResponseHeadersReadCodeFixProvider().RegisterCodeFixesAsync(context);

        var operations = await Assert.Single(actions).GetOperationsAsync(cancellationToken);
        var changedDocument = Assert.Single(operations.OfType<ApplyChangesOperation>())
            .ChangedSolution.GetDocument(document.Id);
        Assert.NotNull(changedDocument);

        return (await changedDocument.GetTextAsync(cancellationToken)).ToString();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AppendsTheOptionWhenTheCallHasNoCancellationToken()
    {
        return CodeFixTestRunner.RunAsync<HttpClientResponseHeadersReadAnalyzer, HttpClientResponseHeadersReadCodeFixProvider>(
            """
            using System.Net.Http;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client)
                {
                    var response = await {|E128010:client.GetAsync("https://example.com")|};
                }
            }
            """,
            """
            using System.Net.Http;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client)
                {
                    var response = await client.GetAsync("https://example.com", HttpCompletionOption.ResponseHeadersRead);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task Fix_LeavesACallWhoseNamedArgumentsWouldNotBindAfterTheInsert()
    {
        const string Code = """
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                async Task M(HttpClient client, CancellationToken ct)
                {
                    await client.GetAsync(cancellationToken: ct, requestUri: "https://example.com");
                }
            }
            """;

        Assert.Equal(Code, await ApplyOfferedFixAsync(Code));
    }
}
