using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SequentialRenameFixAllProviderBranchCoverageMoreTests
{
    private const string Source = "class C { int a = 1; int b = a; }";

    private static async Task<CodeAction?> GetFixAsync(
        Func<Diagnostic, string?, string?> computeNewName,
        Func<SyntaxNode, SyntaxNode> pickTarget)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("P", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var document = project.AddDocument("Test.cs", Source);
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(root);

        var diagnostics = ImmutableArray<Diagnostic>.Empty;
        if (!ReferenceEquals(pickTarget(root), root))
        {
            var target = pickTarget(root);
            diagnostics = [Diagnostic.Create(FakeNamingViolationAnalyzer.Rule, target.GetLocation())];
        }

        var context = new FixAllContext(
            document,
            new NamingStyleCodeFixProvider(),
            FixAllScope.Document,
            null,
            ["IDE1006"],
            new StaticDiagnosticProvider(diagnostics),
            TestContext.Current.CancellationToken);

        var provider = new SequentialRenameFixAllProvider(computeNewName, "test");
        return await provider.GetFixAsync(context);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GetFix_ReturnsNoActionWhenTheNameCalculationYieldsNoName()
    {
        var action = await GetFixAsync(static (_, _) => null, static root => root.DescendantNodes().OfType<VariableDeclaratorSyntax>().First());

        Assert.Null(action);
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task GetFix_ReturnsNoActionWhenTheDiagnosticSitsOnANodeWithoutASymbol()
    {
        var action = await GetFixAsync(static (_, name) => name + "X", static root => root.DescendantNodes().OfType<LiteralExpressionSyntax>().First());

        Assert.Null(action);
    }

    private sealed class StaticDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        private readonly ImmutableArray<Diagnostic> diagnostics;

        internal StaticDiagnosticProvider(ImmutableArray<Diagnostic> diagnostics)
        {
            this.diagnostics = diagnostics;
        }

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
        }

        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
        }
    }
}
