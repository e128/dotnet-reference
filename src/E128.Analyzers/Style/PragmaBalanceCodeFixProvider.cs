using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Style;

/// <summary>
/// Code fix for E128055: inserts a matching <c>#pragma warning restore X</c>
/// at the end of the file so the suppression scope is bounded.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PragmaBalanceCodeFixProvider))]
[Shared]
public sealed class PragmaBalanceCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [PragmaBalanceAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(PragmaBalanceAnalyzer.DiagnosticIdKey, out var suppressedId)
            || suppressedId is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add #pragma warning restore {suppressedId}",
                createChangedDocument: ct => AddRestorePragmaAsync(context.Document, suppressedId, ct),
                equivalenceKey: $"{nameof(PragmaBalanceCodeFixProvider)}.{suppressedId}"),
            diagnostic);
    }

    private static async Task<Document> AddRestorePragmaAsync(
        Document document,
        string suppressedId,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return document;
        }

        var restoreDirective = SyntaxFactory.PragmaWarningDirectiveTrivia(
            SyntaxFactory.Token(SyntaxKind.HashToken),
            SyntaxFactory.Token(SyntaxKind.PragmaKeyword).WithLeadingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.WarningKeyword).WithLeadingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.RestoreKeyword).WithLeadingTrivia(SyntaxFactory.Space),
            SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                SyntaxFactory.IdentifierName(suppressedId).WithLeadingTrivia(SyntaxFactory.Space)),
            SyntaxFactory.Token(SyntaxKind.EndOfDirectiveToken).WithTrailingTrivia(SyntaxFactory.LineFeed),
            isActive: true);

        // Append the restore directive as trailing trivia of the last real token.
        // Attaching to the last token's trailing list avoids the EndOfFileToken
        // interaction that can produce an extra blank line at the end of the file.
        var lastToken = compilationUnit.GetLastToken();
        var newTrailing = lastToken.TrailingTrivia.Add(SyntaxFactory.Trivia(restoreDirective));
        var newToken = lastToken.WithTrailingTrivia(newTrailing);
        var newRoot = compilationUnit.ReplaceToken(lastToken, newToken);
        return document.WithSyntaxRoot(newRoot);
    }
}
