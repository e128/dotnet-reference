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
public sealed class ProcessStartInfoArgumentCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ProcessStartInfoArgumentAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan) is not AssignmentExpressionSyntax assignment)
        {
            return Task.CompletedTask;
        }

        // A nested collection initializer fits an object initializer only. The statement form
        // `psi.Arguments = value;` has no equivalent member-initializer rewrite.
        if (assignment.Parent is not InitializerExpressionSyntax)
        {
            return Task.CompletedTask;
        }

        var tokens = ArgumentTokenizer.TryTokenize(assignment.Right);
        if (tokens is null)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use ArgumentList instead of Arguments",
                ct => ReplaceWithArgumentListAsync(context.Document, assignment, tokens, ct),
                nameof(ProcessStartInfoArgumentCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
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
