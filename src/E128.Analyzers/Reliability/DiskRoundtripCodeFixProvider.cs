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
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128064: replaces the flagged read expression with the in-memory source
///     value captured by the matching write. Handles same-kind replacement (text→text, bytes→bytes)
///     and cross-kind conversion (text↔bytes via <c>Encoding.UTF8</c>). If the read was awaited,
///     the whole <c>await ...</c> expression is replaced so the surrounding statement remains valid.
///     Not registered when the write has no recoverable source expression (multi-write streams).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DiskRoundtripCodeFixProvider))]
[Shared]
public sealed class DiskRoundtripCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DiskRoundtripAnalyzer.DiagnosticId];

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

        if (!diagnostic.Properties.TryGetValue(DiskRoundtripAnalyzer.PropSourceExpression, out var sourceExpr) || string.IsNullOrEmpty(sourceExpr))
        {
            return;
        }

        diagnostic.Properties.TryGetValue(DiskRoundtripAnalyzer.PropWriteKind, out var writeKind);
        diagnostic.Properties.TryGetValue(DiskRoundtripAnalyzer.PropReadKind, out var readKind);
        diagnostic.Properties.TryGetValue(DiskRoundtripAnalyzer.PropIsAwaited, out var isAwaitedStr);
        var isAwaited = string.Equals(isAwaitedStr, "true", StringComparison.Ordinal);

        var targetNode = FindReplacementTarget(root, diagnostic.Location.SourceSpan, isAwaited);
        if (targetNode is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Replace disk read with in-memory value",
                ct => ReplaceReadAsync(context.Document, targetNode, sourceExpr!, writeKind, readKind, ct),
                nameof(DiskRoundtripCodeFixProvider)),
            diagnostic);
    }

    private static SyntaxNode? FindReplacementTarget(SyntaxNode root, TextSpan span, bool isAwaited)
    {
        var node = root.FindNode(span, getInnermostNodeForTie: true);
        return isAwaited
            ? node.FirstAncestorOrSelf<AwaitExpressionSyntax>() ?? node
            : node;
    }

    private static async Task<Document> ReplaceReadAsync(
        Document document,
        SyntaxNode targetNode,
        string sourceExpressionText,
        string? writeKind,
        string? readKind,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var replacement = BuildReplacementExpression(sourceExpressionText, writeKind, readKind)
            .WithTriviaFrom(targetNode);

        var newRoot = root.ReplaceNode(targetNode, replacement);

        if (NeedsSystemTextImport(writeKind, readKind)
            && newRoot is CompilationUnitSyntax compilationUnit)
        {
            newRoot = EnsureUsingDirective(compilationUnit, "System.Text");
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static ExpressionSyntax BuildReplacementExpression(string sourceExpressionText, string? writeKind, string? readKind)
    {
        var sourceExpr = SyntaxFactory.ParseExpression(sourceExpressionText);

        if (writeKind is null || readKind is null || string.Equals(writeKind, readKind, StringComparison.Ordinal))
        {
            return sourceExpr;
        }

        // Text write → bytes read: wrap with Encoding.UTF8.GetBytes(...)
        if (string.Equals(writeKind, "Text", StringComparison.Ordinal)
            && string.Equals(readKind, "Bytes", StringComparison.Ordinal))
        {
            return InvokeStatic("Encoding", "UTF8", "GetBytes", sourceExpr);
        }

        // Bytes write → text read: wrap with Encoding.UTF8.GetString(...)
        if (string.Equals(writeKind, "Bytes", StringComparison.Ordinal)
            && string.Equals(readKind, "Text", StringComparison.Ordinal))
        {
            return InvokeStatic("Encoding", "UTF8", "GetString", sourceExpr);
        }

        // Fall back to direct replacement — analyzer already filtered unfixable cases.
        return sourceExpr;
    }

    private static InvocationExpressionSyntax InvokeStatic(string typeName, string staticMember, string methodName, ExpressionSyntax arg)
    {
        var typeMember = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName(typeName),
            SyntaxFactory.IdentifierName(staticMember));

        var target = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            typeMember,
            SyntaxFactory.IdentifierName(methodName));

        return SyntaxFactory.InvocationExpression(
            target,
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(arg))));
    }

    private static bool NeedsSystemTextImport(string? writeKind, string? readKind)
    {
        return (string.Equals(writeKind, "Text", StringComparison.Ordinal) && string.Equals(readKind, "Bytes", StringComparison.Ordinal))
               || (string.Equals(writeKind, "Bytes", StringComparison.Ordinal) && string.Equals(readKind, "Text", StringComparison.Ordinal));
    }

    private static CompilationUnitSyntax EnsureUsingDirective(CompilationUnitSyntax unit, string namespaceName)
    {
        foreach (var existing in unit.Usings)
        {
            if (existing.Name is not null && string.Equals(existing.Name.ToString(), namespaceName, StringComparison.Ordinal))
            {
                return unit;
            }
        }

        var newUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName));
        return unit.AddUsings(newUsing);
    }
}
