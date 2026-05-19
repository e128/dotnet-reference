using System;
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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FipsUnapprovedHashCodeFixProvider))]
[Shared]
public sealed class FipsUnapprovedHashCodeFixProvider : CodeFixProvider
{
    private static readonly ImmutableDictionary<string, string> ReplacementMap =
        ImmutableDictionary.CreateRange(StringComparer.Ordinal, new[]
        {
            new KeyValuePair<string, string>("MD5", "SHA256"),
            new KeyValuePair<string, string>("SHA1", "SHA256"),
            new KeyValuePair<string, string>("HMACMD5", "HMACSHA256"),
            new KeyValuePair<string, string>("HMACSHA1", "HMACSHA256")
        });

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
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess })
        {
            var receiverName = GetIdentifierName(memberAccess.Expression);
            if (receiverName is not null && ReplacementMap.TryGetValue(receiverName, out var replacement))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Replace with {replacement}",
                        ct => ReplaceReceiverAsync(context.Document, memberAccess, replacement, ct),
                        nameof(FipsUnapprovedHashCodeFixProvider)),
                    diagnostic);
            }
        }
        else if (node is ObjectCreationExpressionSyntax creation)
        {
            var typeName = GetIdentifierName(creation.Type);
            if (typeName is not null && ReplacementMap.TryGetValue(typeName, out var replacement))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Replace with {replacement}",
                        ct => ReplaceConstructorTypeAsync(context.Document, creation, replacement, ct),
                        nameof(FipsUnapprovedHashCodeFixProvider)),
                    diagnostic);
            }
        }
    }

    private static string? GetIdentifierName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            _ => null
        };
    }

    private static string? GetIdentifierName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            _ => null
        };
    }

    private static async Task<Document> ReplaceReceiverAsync(
        Document document,
        MemberAccessExpressionSyntax memberAccess,
        string replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newReceiver = SyntaxFactory.IdentifierName(replacement)
            .WithLeadingTrivia(memberAccess.Expression.GetLeadingTrivia())
            .WithTrailingTrivia(memberAccess.Expression.GetTrailingTrivia());

        var newMemberAccess = memberAccess.WithExpression(newReceiver);
        var newRoot = root.ReplaceNode(memberAccess, newMemberAccess);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> ReplaceConstructorTypeAsync(
        Document document,
        ObjectCreationExpressionSyntax creation,
        string replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newType = SyntaxFactory.IdentifierName(replacement)
            .WithLeadingTrivia(creation.Type.GetLeadingTrivia())
            .WithTrailingTrivia(creation.Type.GetTrailingTrivia());

        var newCreation = creation.WithType(newType);
        var newRoot = root.ReplaceNode(creation, newCreation);
        return document.WithSyntaxRoot(newRoot);
    }
}
