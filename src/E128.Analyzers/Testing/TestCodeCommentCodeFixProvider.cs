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
        var leading = owningToken.LeadingTrivia;

        var index = leading.IndexOf(commentTrivia);
        if (index < 0)
        {
            return document;
        }

        var start = index;
        if (start > 0 && leading[start - 1].IsKind(SyntaxKind.WhitespaceTrivia))
        {
            start--;
        }

        var end = index;
        if (end + 1 < leading.Count && leading[end + 1].IsKind(SyntaxKind.EndOfLineTrivia))
        {
            end++;
        }

        var kept = new List<SyntaxTrivia>();
        for (var i = 0; i < leading.Count; i++)
        {
            if (i < start || i > end)
            {
                kept.Add(leading[i]);
            }
        }

        var newToken = owningToken.WithLeadingTrivia(SyntaxFactory.TriviaList(kept));
        var newRoot = root.ReplaceToken(owningToken, newToken);
        return document.WithSyntaxRoot(newRoot);
    }
}
