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

namespace E128.Analyzers.Security;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FipsUnapprovedHashCodeFixProvider))]
[Shared]
public sealed class FipsUnapprovedHashCodeFixProvider : CodeFixProvider
{
    private static readonly ImmutableHashSet<string> FixableTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "MD5",
        "SHA1");

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [FipsUnapprovedHashAnalyzer.DiagnosticId];

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
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var receiverName = GetReceiverTypeName(memberAccess.Expression);
        if (receiverName is null || !FixableTypes.Contains(receiverName))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Replace with SHA256",
                ct => ReplaceReceiverAsync(context.Document, memberAccess, ct),
                nameof(FipsUnapprovedHashCodeFixProvider)),
            diagnostic);
    }

    private static string? GetReceiverTypeName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            _ => null
        };
    }

    private static async Task<Document> ReplaceReceiverAsync(
        Document document,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newReceiver = SyntaxFactory.IdentifierName("SHA256")
            .WithLeadingTrivia(memberAccess.Expression.GetLeadingTrivia())
            .WithTrailingTrivia(memberAccess.Expression.GetTrailingTrivia());

        var newMemberAccess = memberAccess.WithExpression(newReceiver);
        var newRoot = root.ReplaceNode(memberAccess, newMemberAccess);
        return document.WithSyntaxRoot(newRoot);
    }
}
