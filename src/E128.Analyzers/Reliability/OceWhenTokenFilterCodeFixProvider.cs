using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OceWhenTokenFilterCodeFixProvider))]
[Shared]
public sealed class OceWhenTokenFilterCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [OceWhenTokenFilterAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var catchClause = node.FirstAncestorOrSelf<CatchClauseSyntax>();
        if (catchClause?.Filter is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove negated token-state filter",
                ct => RemoveFilterAsync(context.Document, root, catchClause, ct),
                nameof(OceWhenTokenFilterCodeFixProvider)),
            diagnostic);
    }

    private static Task<Document> RemoveFilterAsync(
        Document document,
        SyntaxNode root,
        CatchClauseSyntax catchClause,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var newCatchClause = catchClause
            .WithFilter(null)
            .WithDeclaration(catchClause.Declaration!.WithTrailingTrivia(catchClause.Filter!.GetTrailingTrivia()));

        var newRoot = root.ReplaceNode(catchClause, newCatchClause);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
