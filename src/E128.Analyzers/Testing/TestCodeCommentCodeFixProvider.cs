using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Testing;

/// <summary>
///     Code fix for E128097: removes the reported comment trivia, along with its own
///     leading indentation and trailing newline, leaving no blank line behind.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TestCodeCommentCodeFixProvider))]
[Shared]
public sealed class TestCodeCommentCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TestCodeCommentAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove comment",
                ct => RemoveCommentAsync(context.Document, diagnostic.Location.SourceSpan, ct),
                nameof(TestCodeCommentCodeFixProvider)),
            diagnostic);
        return Task.CompletedTask;
    }

    private static async Task<Document> RemoveCommentAsync(
        Document document,
        TextSpan commentSpan,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var commentTrivia = root.FindTrivia(commentSpan.Start);
        var owningToken = commentTrivia.Token;

        // A comment sits in the leading list when it has its own line, and in the trailing list when
        // it follows code on the same line.
        var leading = owningToken.LeadingTrivia;
        var index = leading.IndexOf(commentTrivia);
        if (index >= 0)
        {
            var kept = CopyWithoutComment(leading, index, removeLineFeed: true);
            return document.WithSyntaxRoot(
                root.ReplaceToken(owningToken, owningToken.WithLeadingTrivia(SyntaxFactory.TriviaList(kept))));
        }

        var trailing = owningToken.TrailingTrivia;
        index = trailing.IndexOf(commentTrivia);
        if (index < 0)
        {
            return document;
        }

        // The line break after a trailing comment ends the line of code. Keep it.
        var keptTrailing = CopyWithoutComment(trailing, index, removeLineFeed: false);
        return document.WithSyntaxRoot(
            root.ReplaceToken(owningToken, owningToken.WithTrailingTrivia(SyntaxFactory.TriviaList(keptTrailing))));
    }

    private static List<SyntaxTrivia> CopyWithoutComment(
        SyntaxTriviaList trivia,
        int index,
        bool removeLineFeed)
    {
        var start = index > 0 && trivia[index - 1].IsKind(SyntaxKind.WhitespaceTrivia) ? index - 1 : index;
        var end = index;
        if (removeLineFeed && end + 1 < trivia.Count && trivia[end + 1].IsKind(SyntaxKind.EndOfLineTrivia))
        {
            end++;
        }

        var kept = new List<SyntaxTrivia>(trivia.Count);
        for (var i = 0; i < trivia.Count; i++)
        {
            if (i < start || i > end)
            {
                kept.Add(trivia[i]);
            }
        }

        return kept;
    }
}
