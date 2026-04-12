using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OrderByFirstCodeFixProvider))]
[Shared]
public sealed class OrderByFirstCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [OrderByFirstAnalyzer.DiagnosticId];

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

        if (node is not InvocationExpressionSyntax outerInvocation)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace with MinBy/MaxBy",
                createChangedDocument: ct => ApplyFixAsync(context.Document, outerInvocation, ct),
                equivalenceKey: nameof(OrderByFirstCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        InvocationExpressionSyntax outerInvocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (outerInvocation.Expression is not MemberAccessExpressionSyntax outerAccess)
        {
            return document;
        }

        if (outerAccess.Expression is not InvocationExpressionSyntax innerInvocation)
        {
            return document;
        }

        if (innerInvocation.Expression is not MemberAccessExpressionSyntax innerAccess)
        {
            return document;
        }

        var innerName = innerAccess.Name.Identifier.ValueText;
        var isDescending = string.Equals(innerName, "OrderByDescending", StringComparison.Ordinal);
        var replacementName = isDescending ? "MaxBy" : "MinBy";

        var receiver = innerAccess.Expression;
        var keySelector = innerInvocation.ArgumentList;

        var newAccess = innerAccess
            .WithExpression(receiver)
            .WithName(innerAccess.Name.WithIdentifier(
                SyntaxFactory.Identifier(replacementName)));

        var newInvocation = SyntaxFactory
            .InvocationExpression(newAccess, keySelector)
            .WithTriviaFrom(outerInvocation);

        var newRoot = root.ReplaceNode(outerInvocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
