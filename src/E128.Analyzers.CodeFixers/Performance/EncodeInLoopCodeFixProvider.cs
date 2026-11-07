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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EncodeInLoopCodeFixProvider))]
[Shared]
public sealed class EncodeInLoopCodeFixProvider : E128CodeFixProviderBase
{
    private const string Title = "Use GetByteCount";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [EncodeInLoopAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || invocation.Parent is not MemberAccessExpressionSyntax lengthAccess)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                cancellationToken => ApplyFixAsync(
                    context.Document,
                    lengthAccess,
                    access,
                    invocation.ArgumentList,
                    cancellationToken),
                nameof(EncodeInLoopCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        MemberAccessExpressionSyntax lengthAccess,
        MemberAccessExpressionSyntax getBytesAccess,
        ArgumentListSyntax argumentList,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var memberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            getBytesAccess.Expression.WithoutTrivia(),
            SyntaxFactory.IdentifierName("GetByteCount"));
        var replacement = SyntaxFactory
            .InvocationExpression(memberAccess, argumentList.WithoutTrivia())
            .WithTriviaFrom(lengthAccess);

        return document.WithSyntaxRoot(root.ReplaceNode(lengthAccess, replacement));
    }
}
