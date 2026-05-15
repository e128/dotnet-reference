using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(HttpClientMissingOceCatchCodeFixProvider))]
[Shared]
public sealed class HttpClientMissingOceCatchCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [HttpClientMissingOceCatchAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
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
        if (catchClause is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Insert catch (OperationCanceledException) { throw; }",
                ct => InsertOceCatchAsync(context.Document, catchClause, ct),
                nameof(HttpClientMissingOceCatchCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> InsertOceCatchAsync(
        Document document,
        CatchClauseSyntax diagnosedCatch,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (diagnosedCatch.Parent is not TryStatementSyntax tryStatement)
        {
            return document;
        }

        var leadingTrivia = diagnosedCatch.GetLeadingTrivia();
        var indentation = ExtractIndentation(leadingTrivia);
        var innerIndentation = indentation.Add(SyntaxFactory.Whitespace("    "));

        var oceCatch = SyntaxFactory.CatchClause(
                SyntaxFactory.Token(SyntaxKind.CatchKeyword)
                    .WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.CatchDeclaration(
                        SyntaxFactory.IdentifierName("OperationCanceledException"))
                    .WithOpenParenToken(SyntaxFactory.Token(SyntaxKind.OpenParenToken))
                    .WithCloseParenToken(SyntaxFactory.Token(SyntaxKind.CloseParenToken)),
                null,
                SyntaxFactory.Block(
                        SyntaxFactory.ThrowStatement()
                            .WithLeadingTrivia(SyntaxFactory.TriviaList(innerIndentation))
                            .WithTrailingTrivia(SyntaxFactory.LineFeed))
                    .WithOpenBraceToken(
                        SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                            .WithLeadingTrivia(SyntaxFactory.LineFeed, SyntaxFactory.TriviaList(indentation).Last())
                            .WithTrailingTrivia(SyntaxFactory.LineFeed))
                    .WithCloseBraceToken(
                        SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                            .WithLeadingTrivia(SyntaxFactory.TriviaList(indentation))
                            .WithTrailingTrivia(SyntaxFactory.LineFeed)))
            .WithLeadingTrivia(leadingTrivia);

        var catchIndex = tryStatement.Catches.IndexOf(diagnosedCatch);
        var newCatches = tryStatement.Catches.Insert(catchIndex, oceCatch);
        var newTryStatement = tryStatement.WithCatches(newCatches);

        var newRoot = root.ReplaceNode(tryStatement, newTryStatement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxTriviaList ExtractIndentation(SyntaxTriviaList trivia)
    {
        var whitespace = trivia.Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).ToArray();
        return whitespace.Length > 0
            ? SyntaxFactory.TriviaList(whitespace[whitespace.Length - 1])
            : SyntaxFactory.TriviaList();
    }
}
