using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
/// Code fix for E128057: wraps the flagged cleanup call's containing statement
/// in a <c>try { } catch (System.Exception) { }</c> block within the finally clause.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FinallyBlockCleanupCodeFixProvider))]
[Shared]
public sealed class FinallyBlockCleanupCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [FinallyBlockCleanupAnalyzer.DiagnosticId];

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
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var statement = node.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Wrap cleanup in try/catch(Exception)",
                createChangedDocument: ct => WrapInTryCatchAsync(context.Document, statement, ct),
                equivalenceKey: nameof(FinallyBlockCleanupCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> WrapInTryCatchAsync(
        Document document,
        StatementSyntax statement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var tryBlock = SyntaxFactory.Block(statement.WithoutLeadingTrivia());
        var catchClause = SyntaxFactory.CatchClause(
            SyntaxFactory.CatchDeclaration(
                SyntaxFactory.ParseTypeName("System.Exception")),
            filter: null,
            SyntaxFactory.Block());

        var tryCatch = SyntaxFactory.TryStatement(
                tryBlock,
                SyntaxFactory.SingletonList(catchClause),
                null)
            .WithLeadingTrivia(statement.GetLeadingTrivia())
            .WithTrailingTrivia(statement.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(statement, tryCatch);
        return document.WithSyntaxRoot(newRoot);
    }
}
