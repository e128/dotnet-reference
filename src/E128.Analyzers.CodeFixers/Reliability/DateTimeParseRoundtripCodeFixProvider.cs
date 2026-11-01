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

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DateTimeParseRoundtripCodeFixProvider))]
[Shared]
public sealed class DateTimeParseRoundtripCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DateTimeParseRoundtripAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
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

        if (node is not InvocationExpressionSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add DateTimeStyles.RoundtripKind",
                ct => ApplyFixAsync(context.Document, node, ct),
                nameof(DateTimeParseRoundtripCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode invocationNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var invocation = (InvocationExpressionSyntax)invocationNode;

        var roundtripKindArg = SyntaxFactory.Argument(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("DateTimeStyles"),
                    SyntaxFactory.IdentifierName("RoundtripKind")))
            .WithLeadingTrivia(SyntaxFactory.Space);

        var newArgList = invocation.ArgumentList.AddArguments(roundtripKindArg);
        var newInvocation = invocation.WithArgumentList(newArgList);
        var newRoot = root.ReplaceNode(invocation, newInvocation);

        return await EnsureUsingDirectiveAsync(
            document.WithSyntaxRoot(newRoot),
            "System.Globalization",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Document> EnsureUsingDirectiveAsync(
        Document document,
        string namespaceName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return document;
        }

        foreach (var existing in compilationUnit.Usings)
        {
            if (string.Equals(existing.Name?.ToString(), namespaceName, StringComparison.Ordinal))
            {
                return document;
            }
        }

        var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName));
        var newCompilationUnit = compilationUnit.AddUsings(usingDirective);
        return document.WithSyntaxRoot(newCompilationUnit);
    }
}
