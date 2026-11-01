using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace E128.Analyzers.Style;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NonXmlDocCommentCodeFixProvider))]
[Shared]
public sealed class NonXmlDocCommentCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [NonXmlDocCommentE128Analyzer.DiagnosticId];

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

        foreach (var diagnostic in context.Diagnostics)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove comment",
                    ct => RemoveCommentAsync(context.Document, root, diagnostic, ct),
                    "RemoveNonXmlDocComment"),
                diagnostic);
        }
    }

    private static Task<Document> RemoveCommentAsync(
        Document document,
        SyntaxNode root,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var trivia = root.FindTrivia(diagnostic.Location.SourceSpan.Start);
        if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
        {
            return Task.FromResult(document);
        }

        var token = trivia.Token;
        var leadingTrivia = token.LeadingTrivia;
        var index = leadingTrivia.IndexOf(trivia);
        if (index < 0)
        {
            return Task.FromResult(document);
        }

        var newTrivia = leadingTrivia.RemoveAt(index);

        // Remove the EndOfLine after the comment
        if (index < newTrivia.Count && newTrivia[index].IsKind(SyntaxKind.EndOfLineTrivia))
        {
            newTrivia = newTrivia.RemoveAt(index);
        }

        // Remove the leading whitespace before the comment (same line indent)
        if (index > 0 && newTrivia[index - 1].IsKind(SyntaxKind.WhitespaceTrivia))
        {
            newTrivia = newTrivia.RemoveAt(index - 1);
        }

        var newToken = token.WithLeadingTrivia(newTrivia);
        var newRoot = root.ReplaceToken(token, newToken);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
