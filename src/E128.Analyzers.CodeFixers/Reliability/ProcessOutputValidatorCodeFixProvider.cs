using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ProcessOutputValidatorCodeFixProvider))]
[Shared]
public sealed class ProcessOutputValidatorCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ProcessOutputValidatorAnalyzer.DiagnosticId];

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
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax readCall
            || readCall.ArgumentList.Arguments.FirstOrDefault()?.Expression is not { } pathArg)
        {
            return;
        }

        var waitStatement = FindPrecedingWaitStatement(readCall);
        if (waitStatement is null)
        {
            return;
        }

        var pathText = pathArg.ToString();
        var receiverText = pathArg is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "FullName" } access
            ? access.Expression.ToString()
            : null;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Verify output file exists before use",
                ct => InsertGuardAsync(context.Document, waitStatement, pathText, receiverText, ct),
                nameof(ProcessOutputValidatorCodeFixProvider)),
            diagnostic);
    }

    private static StatementSyntax? FindPrecedingWaitStatement(InvocationExpressionSyntax readCall)
    {
        var block = readCall.FirstAncestorOrSelf<BlockSyntax>();
        if (block is null)
        {
            return null;
        }

        StatementSyntax? lastWait = null;
        foreach (var statement in block.Statements)
        {
            if (statement.SpanStart >= readCall.SpanStart)
            {
                break;
            }

            var hasWait = statement.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && (string.Equals(memberAccess.Name.Identifier.ValueText, "WaitForExit", StringComparison.Ordinal)
                    || string.Equals(memberAccess.Name.Identifier.ValueText, "WaitForExitAsync", StringComparison.Ordinal)));

            if (hasWait)
            {
                lastWait = statement;
            }
        }

        return lastWait;
    }

    private static StatementSyntax BuildGuard(string pathText, string? receiverText)
    {
        var guard = receiverText is not null
            ? $"if (!{receiverText}.Exists) throw new InvalidOperationException($\"Expected output file not created: {{{pathText}}}\");"
            : $"if (!File.Exists({pathText})) throw new InvalidOperationException($\"Expected output file not created: {{{pathText}}}\");";

        return SyntaxFactory.ParseStatement(guard);
    }

    private static async Task<Document> InsertGuardAsync(
        Document document,
        StatementSyntax waitStatement,
        string pathText,
        string? receiverText,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var guard = BuildGuard(pathText, receiverText)
            .WithLeadingTrivia(waitStatement.GetLeadingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.InsertNodesAfter(waitStatement, [guard]);
        return document.WithSyntaxRoot(newRoot);
    }
}
