using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace E128.Analyzers.Style;

/// <summary>
///     Code fix for E128047: appends <c>// Justification: </c> after the <c>#pragma warning disable</c> directive.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SuppressionAuditCodeFixProvider))]
[Shared]
public sealed class SuppressionAuditCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [SuppressionAuditAnalyzer.DiagnosticId];

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

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add justification comment",
                ct => AddJustificationCommentAsync(context.Document, root, diagnostic, ct),
                nameof(SuppressionAuditCodeFixProvider)),
            diagnostic);
    }

    private static Task<Document> AddJustificationCommentAsync(
        Document document,
        SyntaxNode root,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan, true, true);
        if (diagnosticNode is null)
        {
            return Task.FromResult(document);
        }

        // Find the last token in the pragma directive.
        var lastToken = diagnosticNode.GetLastToken();
        if (lastToken == default)
        {
            return Task.FromResult(document);
        }

        // Replace trailing trivia: space, comment, then original EndOfLine.
        var comment = SyntaxFactory.Comment("// Justification: ");
        var newTrailing = SyntaxFactory.TriviaList(SyntaxFactory.Space, comment, SyntaxFactory.LineFeed);
        var newToken = lastToken.WithTrailingTrivia(newTrailing);

        var newRoot = root.ReplaceToken(lastToken, newToken);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
