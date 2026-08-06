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
using Microsoft.CodeAnalysis.Formatting;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ChainedStringReplaceCodeFixProvider))]
[Shared]
public sealed class ChainedStringReplaceCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ChainedStringReplaceAnalyzer.DiagnosticId];

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
        if (root.FindNode(diagnostic.Location.SourceSpan) is not WhileStatementSyntax whileStatement)
        {
            return;
        }

        if (!TryGetCollapseShape(whileStatement, out var receiverName, out var collapseChar))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Collapse repeated-character loop into a single pass",
                ct => RewriteAsSinglePassAsync(context.Document, whileStatement, receiverName, collapseChar, ct),
                nameof(ChainedStringReplaceCodeFixProvider)),
            diagnostic);
    }

    // Only the doubled-character collapse shape is provably rewritable from syntax alone.
    // Any other search/replacement literal pair needs runtime knowledge this analysis cannot
    // see, so it stays diagnostic-only.
    private static bool TryGetCollapseShape(WhileStatementSyntax whileStatement, out string receiverName, out char collapseChar)
    {
        receiverName = string.Empty;
        collapseChar = default;

        if (whileStatement.Condition is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "Contains",
                    Expression: IdentifierNameSyntax conditionReceiver
                },
                ArgumentList.Arguments: { Count: > 0 } containsArgs
            } || containsArgs[0].Expression is not LiteralExpressionSyntax { Token.ValueText: var searchLiteral })
        {
            return false;
        }

        if (FindReplaceAssignment(whileStatement.Statement) is not { } assignment
            || assignment.Left is not IdentifierNameSyntax assignmentTarget
            || !string.Equals(assignmentTarget.Identifier.ValueText, conditionReceiver.Identifier.ValueText, StringComparison.Ordinal)
            || assignment.Right is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "Replace",
                    Expression: IdentifierNameSyntax replaceReceiver
                },
                ArgumentList.Arguments: { Count: > 1 } replaceArgs
            }
            || !string.Equals(replaceReceiver.Identifier.ValueText, conditionReceiver.Identifier.ValueText, StringComparison.Ordinal)
            || replaceArgs[0].Expression is not LiteralExpressionSyntax { Token.ValueText: var replaceSearchLiteral }
            || replaceArgs[1].Expression is not LiteralExpressionSyntax { Token.ValueText: var replacementLiteral })
        {
            return false;
        }

        if (!string.Equals(searchLiteral, replaceSearchLiteral, StringComparison.Ordinal)
            || replacementLiteral.Length != 1
            || !string.Equals(searchLiteral, replacementLiteral + replacementLiteral, StringComparison.Ordinal))
        {
            return false;
        }

        receiverName = conditionReceiver.Identifier.ValueText;
        collapseChar = replacementLiteral[0];
        return true;
    }

    private static AssignmentExpressionSyntax? FindReplaceAssignment(StatementSyntax body)
    {
        foreach (var statement in body is BlockSyntax block ? block.Statements : new SyntaxList<StatementSyntax>(body))
        {
            if (statement is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment })
            {
                return assignment;
            }
        }

        return null;
    }

    private static async Task<Document> RewriteAsSinglePassAsync(
        Document document,
        WhileStatementSyntax whileStatement,
        string receiverName,
        char collapseChar,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var escapedChar = collapseChar is '\'' or '\\' ? "\\" + collapseChar : collapseChar.ToString();
        var replacement = SyntaxFactory.ParseStatement($$"""
            {
                var __buffer = new char[{{receiverName}}.Length];
                var __pos = 0;
                var __collapsing = false;
                foreach (var __c in {{receiverName}})
                {
                    if (__c == '{{escapedChar}}')
                    {
                        if (!__collapsing)
                        {
                            __buffer[__pos++] = __c;
                            __collapsing = true;
                        }
                    }
                    else
                    {
                        __buffer[__pos++] = __c;
                        __collapsing = false;
                    }
                }

                {{receiverName}} = new string(__buffer, 0, __pos);
            }
            """)
            .WithLeadingTrivia(whileStatement.GetLeadingTrivia())
            .WithTrailingTrivia(whileStatement.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.ReplaceNode(whileStatement, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}
