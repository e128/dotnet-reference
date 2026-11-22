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
    private const int TwoArgumentReplaceCount = 2;

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ChainedStringReplaceAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan) is not WhileStatementSyntax whileStatement)
        {
            return Task.CompletedTask;
        }

        var shape = GetShape(
            whileStatement,
            out var receiverName,
            out var searchLiteral,
            out var replacementLiteral);

        if (shape is CollapseShape.DoubledCharacter)
        {
            CodeFixRegistration.Register(
                context,
                diagnostic,
                "Collapse repeated-character loop into a single pass",
                nameof(ChainedStringReplaceCodeFixProvider),
                ct => RewriteAsSinglePassAsync(
                    context.Document,
                    whileStatement,
                    receiverName,
                    replacementLiteral[0],
                    ct));
        }
        else if (shape is CollapseShape.SinglePassReplace)
        {
            CodeFixRegistration.Register(
                context,
                diagnostic,
                "Replace once instead of looping",
                nameof(ChainedStringReplaceCodeFixProvider),
                ct => RewriteAsSingleReplaceAsync(
                    context.Document,
                    whileStatement,
                    receiverName,
                    searchLiteral,
                    replacementLiteral,
                    ct));
        }

        return Task.CompletedTask;
    }

    private static CollapseShape GetShape(
        WhileStatementSyntax whileStatement,
        out string receiverName,
        out string searchLiteral,
        out string replacementLiteral)
    {
        receiverName = string.Empty;
        searchLiteral = string.Empty;
        replacementLiteral = string.Empty;

        if (!TryGetContainsCondition(whileStatement.Condition, out var conditionReceiver, out var conditionSearch)
            || !TryGetReplaceLiterals(
                whileStatement.Statement,
                conditionReceiver,
                out var replaceSearchLiteral,
                out var replaceLiteral,
                out var replaceArgumentCount))
        {
            return CollapseShape.None;
        }

        if (!string.Equals(conditionSearch, replaceSearchLiteral, StringComparison.Ordinal))
        {
            return CollapseShape.None;
        }

        receiverName = conditionReceiver;
        searchLiteral = conditionSearch;
        replacementLiteral = replaceLiteral;

        if (replaceLiteral.Length is 1
            && string.Equals(conditionSearch, replaceLiteral + replaceLiteral, StringComparison.Ordinal))
        {
            return CollapseShape.DoubledCharacter;
        }

        if (replaceArgumentCount is TwoArgumentReplaceCount
            && conditionSearch.Length is 1
            && replaceLiteral.IndexOf(conditionSearch, StringComparison.Ordinal) < 0)
        {
            return CollapseShape.SinglePassReplace;
        }

        return CollapseShape.None;
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
        out string replacementLiteral,
        out int argumentCount)
    {
        searchLiteral = string.Empty;
        replacementLiteral = string.Empty;
        argumentCount = 0;

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
        argumentCount = replaceArgs.Count;
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

    private enum CollapseShape
    {
        None = 0,
        DoubledCharacter = 1,
        SinglePassReplace = 2,
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

    private static async Task<Document> RewriteAsSingleReplaceAsync(
        Document document,
        WhileStatementSyntax whileStatement,
        string receiverName,
        string searchLiteral,
        string replacementLiteral,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var search = SymbolDisplay.FormatLiteral(searchLiteral, quote: true);
        var replacement = SymbolDisplay.FormatLiteral(replacementLiteral, quote: true);
        var text = receiverName + " = " + receiverName
            + ".Replace(" + search + ", " + replacement + ");";

        var statement = SyntaxFactory.ParseStatement(text)
            .WithLeadingTrivia(whileStatement.GetLeadingTrivia())
            .WithTrailingTrivia(whileStatement.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.ReplaceNode(whileStatement, statement);
        return document.WithSyntaxRoot(newRoot);
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

        var charLiteral = SymbolDisplay.FormatLiteral(collapseChar, true);
        var replacement = SyntaxFactory.ParseStatement(BuildCollapseBlock(receiverName, charLiteral))
            .WithLeadingTrivia(whileStatement.GetLeadingTrivia())
            .WithTrailingTrivia(whileStatement.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.ReplaceNode(whileStatement, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}
