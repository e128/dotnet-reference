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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sha256CreateObsoleteCodeFixProvider))]
[Shared]
public sealed class Sha256CreateObsoleteCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [Sha256CreateObsoleteAnalyzer.DiagnosticId];

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
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax createInvocation)
        {
            return;
        }

        if (!IsChainedWithComputeHash(createInvocation, out _))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use SHA256.HashData()",
                ct => RewriteToHashDataAsync(context.Document, createInvocation, ct),
                nameof(Sha256CreateObsoleteCodeFixProvider)),
            diagnostic);
    }

    private static bool IsChainedWithComputeHash(
        InvocationExpressionSyntax createInvocation,
        out InvocationExpressionSyntax? outerInvocation)
    {
        outerInvocation = null;

        if (createInvocation.Parent is not MemberAccessExpressionSyntax outerAccess)
        {
            return false;
        }

        if (!string.Equals(outerAccess.Name.Identifier.ValueText, "ComputeHash", StringComparison.Ordinal))
        {
            return false;
        }

        if (outerAccess.Parent is not InvocationExpressionSyntax outer)
        {
            return false;
        }

        outerInvocation = outer;
        return true;
    }

    private static async Task<Document> RewriteToHashDataAsync(
        Document document,
        InvocationExpressionSyntax createInvocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || !IsChainedWithComputeHash(createInvocation, out var outerInvocation) || outerInvocation is null)
        {
            return document;
        }

        if (createInvocation.Expression is not MemberAccessExpressionSyntax createAccess)
        {
            return document;
        }

        var hashDataAccess = createAccess.WithName(
            createAccess.Name.WithIdentifier(
                SyntaxFactory.Identifier("HashData")));

        var hashDataInvocation = SyntaxFactory.InvocationExpression(
                hashDataAccess,
                outerInvocation.ArgumentList)
            .WithLeadingTrivia(outerInvocation.GetLeadingTrivia())
            .WithTrailingTrivia(outerInvocation.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(outerInvocation, hashDataInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
