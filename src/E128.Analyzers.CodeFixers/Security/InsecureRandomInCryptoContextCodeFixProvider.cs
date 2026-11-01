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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InsecureRandomInCryptoContextCodeFixProvider))]
[Shared]
public sealed class InsecureRandomInCryptoContextCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [InsecureRandomInCryptoContextAnalyzer.DiagnosticId];

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

        if (node is ObjectCreationExpressionSyntax)
        {
            return;
        }

        if (node is not MemberAccessExpressionSyntax randomSharedAccess)
        {
            return;
        }

        var match = TryGetFixableInvocation(randomSharedAccess);
        if (match is null)
        {
            return;
        }

        var (outerInvocation, methodName) = match.Value;

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Use RandomNumberGenerator.{methodName}()",
                ct => ReplaceWithRandomNumberGeneratorAsync(
                    context.Document, outerInvocation, methodName, ct),
                nameof(InsecureRandomInCryptoContextCodeFixProvider)),
            diagnostic);
    }

    private static (InvocationExpressionSyntax Invocation, string MethodName)? TryGetFixableInvocation(
        MemberAccessExpressionSyntax randomSharedAccess)
    {
        return randomSharedAccess.Parent is MemberAccessExpressionSyntax methodAccess
               && string.Equals(methodAccess.Name.Identifier.ValueText, "Next", StringComparison.Ordinal)
               && methodAccess.Parent is InvocationExpressionSyntax invocation
               && invocation.ArgumentList.Arguments.Count is 1 or 2
            ? (invocation, "GetInt32")
            : null;
    }

    private static async Task<Document> ReplaceWithRandomNumberGeneratorAsync(
        Document document,
        InvocationExpressionSyntax outerInvocation,
        string methodName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newExpression = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName("RandomNumberGenerator"),
            SyntaxFactory.IdentifierName(methodName));

        var newInvocation = SyntaxFactory.InvocationExpression(newExpression, outerInvocation.ArgumentList)
            .WithLeadingTrivia(outerInvocation.GetLeadingTrivia())
            .WithTrailingTrivia(outerInvocation.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(outerInvocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
