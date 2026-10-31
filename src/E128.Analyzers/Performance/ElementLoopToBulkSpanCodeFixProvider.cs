using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

/// <summary>
///     Code fix for E128140: rewrites the element loop as a <c lang="csharp">Fill</c>,
///     <c lang="csharp">Clear</c>, or <c lang="csharp">CopyTo</c> call on a span.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ElementLoopToBulkSpanCodeFixProvider))]
[Shared]
public sealed class ElementLoopToBulkSpanCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ElementLoopToBulkSpanAnalyzer.DiagnosticId];

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
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        if (node is not ForStatementSyntax forStatement)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var spanType = semanticModel?.Compilation.GetTypeByMetadataName("System.Span`1");

        if (semanticModel is null || spanType is null)
        {
            return;
        }

        if (ElementLoopToBulkSpanAnalyzer.TryMatch(forStatement, semanticModel, spanType, context.CancellationToken)
            is not { } match)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use Span." + match.BulkMethod,
                _ => Task.FromResult(ApplyFix(context.Document, root, forStatement, match)),
                nameof(ElementLoopToBulkSpanCodeFixProvider)),
            diagnostic);
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        ForStatementSyntax forStatement,
        ElementLoopToBulkSpanAnalyzer.BulkLoopMatch match)
    {
        var destination = match.Destination.ToString();
        var value = match.Value?.ToString();
        var source = match.Source?.ToString();

        var text = match.Kind switch
        {
            ElementLoopToBulkSpanAnalyzer.LoopKind.Clear =>
                AsSpan(match.DestinationIsSpan, destination) + ".Clear();",
            ElementLoopToBulkSpanAnalyzer.LoopKind.Fill when value is not null =>
                AsSpan(match.DestinationIsSpan, destination) + ".Fill(" + value + ");",
            ElementLoopToBulkSpanAnalyzer.LoopKind.Copy when source is not null =>
                AsSpan(match.SourceIsSpan, source) + ".CopyTo(" + destination + ");",
            _ => null,
        };

        if (text is null)
        {
            return document;
        }

        var replacement = SyntaxFactory.ParseStatement(text).WithTriviaFrom(forStatement);
        var newRoot = root.ReplaceNode(forStatement, replacement);

        if (text.Contains(".AsSpan()", StringComparison.Ordinal))
        {
            newRoot = UsingDirectives.AddIfMissing(newRoot, "System");
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static string AsSpan(bool alreadySpan, string expression)
    {
        return alreadySpan ? expression : expression + ".AsSpan()";
    }
}
