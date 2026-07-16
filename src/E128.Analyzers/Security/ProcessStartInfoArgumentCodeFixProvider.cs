using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Security;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ProcessStartInfoArgumentCodeFixProvider))]
[Shared]
public sealed class ProcessStartInfoArgumentCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ProcessStartInfoArgumentAnalyzer.DiagnosticId];

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
        if (root.FindNode(diagnostic.Location.SourceSpan) is not AssignmentExpressionSyntax assignment)
        {
            return;
        }

        var tokens = ArgumentTokenizer.TryTokenize(assignment.Right);
        if (tokens is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use ArgumentList instead of Arguments",
                ct => ReplaceWithArgumentListAsync(context.Document, assignment, tokens, ct),
                nameof(ProcessStartInfoArgumentCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ReplaceWithArgumentListAsync(
        Document document,
        AssignmentExpressionSyntax assignment,
        IReadOnlyList<ExpressionSyntax> tokens,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newAssignment = SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName("ArgumentList"),
                SyntaxFactory.InitializerExpression(
                    SyntaxKind.CollectionInitializerExpression,
                    SyntaxFactory.SeparatedList(tokens)))
            .WithTriviaFrom(assignment);

        var newRoot = root.ReplaceNode(assignment, newAssignment);
        return document.WithSyntaxRoot(newRoot);
    }
}
