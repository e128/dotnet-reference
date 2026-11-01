using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128110: inserts a whole-array
///     <c lang="csharp">CryptographicOperations.ZeroMemory</c> call before the flagged
///     <c lang="csharp">Return</c> call inside its finally block.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PooledBufferZeroingCodeFixProvider))]
[Shared]
public sealed class PooledBufferZeroingCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [PooledBufferZeroingAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return Task.CompletedTask;
        }

        // The zeroing call clears the buffer on every exit path only when it shares the finally
        // block that holds the Return. A Return with no finally needs a try/finally restructure,
        // so the fix declines it.
        var returnStatement = invocation.FirstAncestorOrSelf<StatementSyntax>();
        if (returnStatement is null
            || returnStatement.Parent is not BlockSyntax block
            || block.Parent is not FinallyClauseSyntax)
        {
            return Task.CompletedTask;
        }

        var bufferText = invocation.ArgumentList.Arguments[0].Expression.ToString();

        context.RegisterCodeFix(
            CodeAction.Create(
                "Zero the buffer before Return",
                ct => ZeroBufferAsync(context.Document, returnStatement, bufferText, ct),
                nameof(PooledBufferZeroingCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Document> ZeroBufferAsync(
        Document document,
        StatementSyntax returnStatement,
        string bufferText,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var zeroStatement = SyntaxFactory
            .ParseStatement($"CryptographicOperations.ZeroMemory({bufferText});")
            .WithLeadingTrivia(returnStatement.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.InsertNodesBefore(returnStatement, [zeroStatement]);
        newRoot = UsingDirectives.AddIfMissing(newRoot, "System.Security.Cryptography");

        return document.WithSyntaxRoot(newRoot);
    }
}
