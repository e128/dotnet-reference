using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Style;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyStringLiteralCodeFixProvider))]
[Shared]
public sealed class EmptyStringLiteralCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [EmptyStringLiteralAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        RegisterNodeFix<LiteralExpressionSyntax>(
            context,
            root,
            "Replace \"\" with string.Empty",
            nameof(EmptyStringLiteralCodeFixProvider),
            (emptyLiteral, ct) => ApplyFixAsync(context.Document, emptyLiteral, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode emptyLiteralNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var stringEmpty = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                SyntaxFactory.IdentifierName("Empty"))
            .WithTriviaFrom(emptyLiteralNode);

        var newRoot = root.ReplaceNode(emptyLiteralNode, stringEmpty);
        return document.WithSyntaxRoot(newRoot);
    }
}
