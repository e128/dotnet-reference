using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Style;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EncodingDefaultCodeFixProvider))]
[Shared]
public sealed class EncodingDefaultCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [EncodingDefaultAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not MemberAccessExpressionSyntax memberAccess)
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Replace Encoding.Default with Encoding.UTF8", nameof(EncodingDefaultCodeFixProvider), ct => ApplyFixAsync(context.Document, memberAccess, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        MemberAccessExpressionSyntax encodingDefaultNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Swap the member name only, so a qualified receiver such as System.Text.Encoding stays.
        var encodingUtf8 = encodingDefaultNode.WithName(
            SyntaxFactory.IdentifierName("UTF8").WithTriviaFrom(encodingDefaultNode.Name));

        var newRoot = root.ReplaceNode(encodingDefaultNode, encodingUtf8);
        return document.WithSyntaxRoot(newRoot);
    }
}
