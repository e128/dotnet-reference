using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ChainedStringReplaceCodeFixProvider))]
[Shared]
public sealed class ChainedStringReplaceCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ChainedStringReplaceAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan) is not WhileStatementSyntax whileStatement)
        {
            return Task.CompletedTask;
        }

        if (!TryGetCollapseShape(whileStatement, out var receiverName, out var collapseChar))
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Collapse repeated-character loop into a single pass", nameof(ChainedStringReplaceCodeFixProvider), ct => RewriteAsSinglePassAsync(context.Document, whileStatement, receiverName, collapseChar, ct));

        return Task.CompletedTask;
    }

    // Only the doubled-character collapse shape is provably rewritable from syntax alone.
    // Any other search/replacement literal pair needs runtime knowledge this analysis cannot
    // see, so it stays diagnostic-only.
    private static bool TryGetCollapseShape(WhileStatementSyntax whileStatement, out string receiverName, out char collapseChar)
    {
        receiverName = string.Empty;
        collapseChar = default;

        if (!TryGetContainsCondition(whileStatement.Condition, out var conditionReceiver, out var searchLiteral)
            || !TryGetReplaceLiterals(
                whileStatement.Statement, conditionReceiver, out var replaceSearchLiteral, out var replacementLiteral))
        {
            return false;
        }

        if (!string.Equals(searchLiteral, replaceSearchLiteral, StringComparison.Ordinal)
            || replacementLiteral.Length is not 1
            || !string.Equals(searchLiteral, replacementLiteral + replacementLiteral, StringComparison.Ordinal))
        {
            return false;
        }

        receiverName = conditionReceiver;
        collapseChar = replacementLiteral[0];
        return true;
    }

    private static bool TryGetContainsCondition(
        ExpressionSyntax condition,
        out string receiver,
        out string searchLiteral)
    {
        receiver = string.Empty;
        searchLiteral = string.Empty;

        if (condition is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "Contains",
                    Expression: IdentifierNameSyntax conditionReceiver
                },
                ArgumentList.Arguments: { Count: > 0 } containsArgs
            } || containsArgs[0].Expression is not LiteralExpressionSyntax { Token.ValueText: var literal })
        {
            return false;
        }

        receiver = conditionReceiver.Identifier.ValueText;
        searchLiteral = literal;
        return true;
    }

    private static bool TryGetReplaceLiterals(
        StatementSyntax body,
        string receiver,
        out string searchLiteral,
        out string replacementLiteral)
    {
        searchLiteral = string.Empty;
        replacementLiteral = string.Empty;

        if (FindReplaceAssignment(body) is not { } assignment
            || assignment.Left is not IdentifierNameSyntax assignmentTarget
            || !string.Equals(assignmentTarget.Identifier.ValueText, receiver, StringComparison.Ordinal)
            || assignment.Right is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "Replace",
                    Expression: IdentifierNameSyntax replaceReceiver
                },
                ArgumentList.Arguments: { Count: > 1 } replaceArgs
            }
            || !string.Equals(replaceReceiver.Identifier.ValueText, receiver, StringComparison.Ordinal)
            || replaceArgs[0].Expression is not LiteralExpressionSyntax { Token.ValueText: var search }
            || replaceArgs[1].Expression is not LiteralExpressionSyntax { Token.ValueText: var replacement })
        {
            return false;
        }

        searchLiteral = search;
        replacementLiteral = replacement;
        return true;
    }

    private static AssignmentExpressionSyntax? FindReplaceAssignment(StatementSyntax body)
    {
        foreach (var statement in body is BlockSyntax block ? block.Statements : SyntaxFactory.SingletonList(body))
        {
            if (statement is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment })
            {
                return assignment;
            }
        }

        return null;
    }

    private static string BuildCollapseBlock(string receiverName, string charLiteral)
    {
        string[] lines =
        [
            "{",
            $"    var __buffer = new char[{receiverName}.Length];",
            "    var __pos = 0;",
            "    var __collapsing = false;",
            $"    foreach (var __c in {receiverName})",
            "    {",
            $"        if (__c == {charLiteral})",
            "        {",
            "            if (!__collapsing)",
            "            {",
            "                __buffer[__pos++] = __c;",
            "                __collapsing = true;",
            "            }",
            "        }",
            "        else",
            "        {",
            "            __buffer[__pos++] = __c;",
            "            __collapsing = false;",
            "        }",
            "    }",
            string.Empty,
            $"    {receiverName} = new string(__buffer, 0, __pos);",
            "}"
        ];

        return string.Join("\n", lines);
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

        // The collapse character is a runtime value. FormatLiteral puts the quotes and the escapes
        // back, so a newline or a quote stays a valid character literal.
        var charLiteral = SymbolDisplay.FormatLiteral(collapseChar, true);
        var replacement = SyntaxFactory.ParseStatement(BuildCollapseBlock(receiverName, charLiteral))
            .WithLeadingTrivia(whileStatement.GetLeadingTrivia())
            .WithTrailingTrivia(whileStatement.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.ReplaceNode(whileStatement, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}
