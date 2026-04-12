using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

/// <summary>
/// Code fix for E128048: converts an if/else-if chain on enum values to a switch statement.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EnumIfElseChainCodeFixProvider))]
[Shared]
public sealed class EnumIfElseChainCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [EnumIfElseChainAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var ifStatement = node.FirstAncestorOrSelf<IfStatementSyntax>();
        if (ifStatement is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Convert to switch statement",
                createChangedDocument: ct => ConvertToSwitchAsync(context.Document, ifStatement, ct),
                equivalenceKey: nameof(EnumIfElseChainCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ConvertToSwitchAsync(
        Document document,
        IfStatementSyntax ifStatement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var switchExpression = ExtractSwitchExpression(ifStatement);
        if (switchExpression is null)
        {
            return document;
        }

        var sections = BuildSwitchSections(ifStatement);
        if (sections is null)
        {
            return document;
        }

        var switchStatement = SyntaxFactory.SwitchStatement(switchExpression)
            .WithSections(SyntaxFactory.List(sections))
            .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        var newRoot = root.ReplaceNode(ifStatement, switchStatement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static List<SwitchSectionSyntax>? BuildSwitchSections(IfStatementSyntax ifStatement)
    {
        var sections = new List<SwitchSectionSyntax>();
        var current = ifStatement;

        while (current is not null)
        {
            var caseLabel = ExtractCaseLabel(current.Condition);
            if (caseLabel is null)
            {
                return null;
            }

            sections.Add(BuildCaseSection(
                SyntaxFactory.CaseSwitchLabel(caseLabel),
                current.Statement));

            if (current.Else?.Statement is IfStatementSyntax elseIf)
            {
                current = elseIf;
            }
            else
            {
                if (current.Else?.Statement is not null)
                {
                    sections.Add(BuildCaseSection(
                        SyntaxFactory.DefaultSwitchLabel(),
                        current.Else.Statement));
                }

                current = null;
            }
        }

        return sections;
    }

    private static SwitchSectionSyntax BuildCaseSection(SwitchLabelSyntax label, StatementSyntax body)
    {
        var statements = new List<StatementSyntax>();
        if (body is BlockSyntax block)
        {
            statements.AddRange(block.Statements);
        }
        else
        {
            statements.Add(body);
        }

        statements.Add(SyntaxFactory.BreakStatement());

        return SyntaxFactory.SwitchSection(
            SyntaxFactory.SingletonList(label),
            SyntaxFactory.List(statements));
    }

    private static ExpressionSyntax? ExtractSwitchExpression(IfStatementSyntax ifStatement)
    {
        // The switch expression is the variable side (not the enum constant).
        // For `x == MyEnum.Value`, the variable is the left side.
        return ifStatement.Condition is BinaryExpressionSyntax binary ? binary.Left : null;
    }

    private static ExpressionSyntax? ExtractCaseLabel(ExpressionSyntax condition)
    {
        return condition is not BinaryExpressionSyntax binary
            ? null
            : binary.Right is MemberAccessExpressionSyntax ? binary.Right : binary.Left is MemberAccessExpressionSyntax ? binary.Left : null;
    }
}
