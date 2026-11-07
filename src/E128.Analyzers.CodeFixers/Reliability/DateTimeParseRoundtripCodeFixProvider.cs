using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DateTimeParseRoundtripCodeFixProvider))]
[Shared]
public sealed class DateTimeParseRoundtripCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DateTimeParseRoundtripAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax)
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Add DateTimeStyles.RoundtripKind", nameof(DateTimeParseRoundtripCodeFixProvider), ct => ApplyFixAsync(context.Document, node, ct));

        return Task.CompletedTask;
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

        return await UsingDirectives.EnsureAddedAsync(
            document.WithSyntaxRoot(newRoot),
            "System.Globalization",
            cancellationToken).ConfigureAwait(false);
    }
}
