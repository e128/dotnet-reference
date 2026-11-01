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
public sealed class HttpClientMissingOceCatchCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [HttpClientMissingOceCatchAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var catchClause = node.FirstAncestorOrSelf<CatchClauseSyntax>();
        if (catchClause is null)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Insert catch (OperationCanceledException) { throw; }",
                ct => InsertOceCatchAsync(context.Document, catchClause, ct),
                nameof(HttpClientMissingOceCatchCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
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
                            .WithLeadingTrivia(BraceLeadingTrivia(indentation))
                            .WithTrailingTrivia(SyntaxFactory.LineFeed))
                    .WithCloseBraceToken(
                        SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                            .WithLeadingTrivia(SyntaxFactory.TriviaList(indentation))
                            .WithTrailingTrivia(SyntaxFactory.LineFeed)))
            .WithLeadingTrivia(leadingTrivia);

        var catchIndex = tryStatement.Catches.IndexOf(diagnosedCatch);
        var newCatches = tryStatement.Catches.Insert(catchIndex, oceCatch);
        var newTryStatement = tryStatement.WithCatches(newCatches);

        var newRoot = UsingDirectives.AddIfMissing(
            root.ReplaceNode(tryStatement, newTryStatement), "System");

        return document.WithSyntaxRoot(newRoot);
    }

    // The indentation list is empty when the catch keyword starts a line with no leading space.
    private static SyntaxTriviaList BraceLeadingTrivia(SyntaxTriviaList indentation)
    {
        return indentation.Count > 0
            ? SyntaxFactory.TriviaList(SyntaxFactory.LineFeed, indentation.Last())
            : SyntaxFactory.TriviaList(SyntaxFactory.LineFeed);
    }

    private static SyntaxTriviaList ExtractIndentation(SyntaxTriviaList trivia)
    {
        var whitespace = trivia.Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).ToArray();
        return whitespace.Length > 0
            ? SyntaxFactory.TriviaList(whitespace[whitespace.Length - 1])
            : SyntaxFactory.TriviaList();
    }
}
