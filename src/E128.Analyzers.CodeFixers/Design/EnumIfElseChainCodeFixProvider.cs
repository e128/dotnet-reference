using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128048: converts an if/else-if chain on enum values to a switch statement.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EnumIfElseChainCodeFixProvider))]
[Shared]
public sealed class EnumIfElseChainCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [EnumIfElseChainAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var ifStatement = node.FirstAncestorOrSelf<IfStatementSyntax>();
        if (ifStatement is null || !IsEqualityChain(ifStatement))
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Convert to switch statement", nameof(EnumIfElseChainCodeFixProvider), ct => ConvertToSwitchAsync(context.Document, ifStatement, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> ConvertToSwitchAsync(
        Document document,
        IfStatementSyntax ifStatement,
        CancellationToken cancellationToken)
    {
        var rootAndModel = await FixSyntax.GetRootAndSemanticModelAsync(document, cancellationToken).ConfigureAwait(false);
        if (rootAndModel is null)
        {
            return document;
        }

        var (root, model) = rootAndModel.Value;

        var switchExpression = ExtractSwitchExpression(ifStatement, model, cancellationToken);
        if (switchExpression is null)
        {
            return document;
        }

        var sections = BuildSwitchSections(ifStatement, model, cancellationToken);
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

    private static List<SwitchSectionSyntax>? BuildSwitchSections(
        IfStatementSyntax ifStatement,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var sections = new List<SwitchSectionSyntax>();
        var current = ifStatement;

        while (current is not null)
        {
            var caseLabel = ExtractCaseLabel(current.Condition, model, cancellationToken);
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

    private static bool IsEqualityChain(IfStatementSyntax ifStatement)
    {
        var current = ifStatement;
        while (current is not null)
        {
            if (current.Condition is not BinaryExpressionSyntax binary
                || !binary.IsKind(SyntaxKind.EqualsExpression))
            {
                return false;
            }

            current = current.Else?.Statement as IfStatementSyntax;
        }

        return true;
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

        if (statements.Count is 0 || !LeavesSwitch(statements[statements.Count - 1]))
        {
            statements.Add(SyntaxFactory.BreakStatement());
        }

        return SyntaxFactory.SwitchSection(
            SyntaxFactory.SingletonList(label),
            SyntaxFactory.List(statements));
    }

    private static bool LeavesSwitch(StatementSyntax statement)
    {
        return statement is ReturnStatementSyntax
            or ThrowStatementSyntax
            or ContinueStatementSyntax
            or BreakStatementSyntax;
    }

    private static ExpressionSyntax? ExtractSwitchExpression(
        IfStatementSyntax ifStatement,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (ifStatement.Condition is not BinaryExpressionSyntax binary)
        {
            return null;
        }

        return IsEnumConstant(binary.Left, model, cancellationToken)
            ? binary.Right
            : binary.Left;
    }

    private static ExpressionSyntax? ExtractCaseLabel(
        ExpressionSyntax condition,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (condition is not BinaryExpressionSyntax binary)
        {
            return null;
        }

        if (IsEnumConstant(binary.Left, model, cancellationToken))
        {
            return binary.Left;
        }

        return IsEnumConstant(binary.Right, model, cancellationToken) ? binary.Right : null;
    }

    private static bool IsEnumConstant(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        return model.GetConstantValue(expression, cancellationToken).HasValue
               && model.GetTypeInfo(expression, cancellationToken).Type is { TypeKind: TypeKind.Enum };
    }
}
