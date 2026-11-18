using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

/// <summary>
///     Code fix for E128231: enumerates <c lang="csharp">source.AsSpan().Split(separator)</c> and
///     reads each part as a slice of the span.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SplitEnumerationCodeFixProvider))]
[Shared]
public sealed class SplitEnumerationCodeFixProvider : E128CodeFixProviderBase
{
    private const string SystemNamespace = "System";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [SplitEnumerationAnalyzer.DiagnosticId];

    /// <inheritdoc />
    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var loop = root.FindNode(context.Diagnostics[0].Location.SourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<ForEachStatementSyntax>()
            .FirstOrDefault();

        if (loop is null)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model is null || SplitEnumerationAnalyzer.CreateTerms(model.Compilation) is not { } terms)
        {
            return;
        }

        if (!SplitEnumerationAnalyzer.TryMatch(loop, model, terms, context.CancellationToken, out var receiver, out var separator)
            || receiver is null
            || separator is null
            || !SplitEnumerationAnalyzer.CanRewrite(loop, model, terms, receiver, context.CancellationToken, out var reads))
        {
            return;
        }

        CodeFixRegistration.Register(
            context,
            context.Diagnostics[0],
            "Enumerate the split ranges over the span",
            SplitEnumerationAnalyzer.DiagnosticId,
            ct => ApplyFixAsync(context.Document, loop, receiver, separator, reads, ct));
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        ForEachStatementSyntax loop,
        ExpressionSyntax receiver,
        ExpressionSyntax separator,
        ImmutableArray<IdentifierNameSyntax> reads,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var receiverText = receiver.WithoutTrivia().ToFullString();
        var elementText = receiverText + ".AsSpan()[" + loop.Identifier.ValueText + "]";

        var body = loop.Statement.ReplaceNodes(
            reads,
            (original, _) => SyntaxFactory.ParseExpression(elementText).WithTriviaFrom(original));

        var asSpanCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression(receiverText),
                SyntaxFactory.IdentifierName("AsSpan")));

        var splitCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                asSpanCall,
                SyntaxFactory.IdentifierName("Split")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(separator.WithoutTrivia()))));

        var newLoop = SyntaxFactory.ForEachStatement(
            SyntaxFactory.IdentifierName("var"),
            loop.Identifier,
            splitCall,
            body);

        var newRoot = UsingDirectives.AddIfMissing(
            root.ReplaceNode(loop, newLoop.WithTriviaFrom(loop)),
            SystemNamespace);

        return document.WithSyntaxRoot(newRoot);
    }
}
