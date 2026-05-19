using System;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ArrayPoolSqliteParameterSizeCodeFixProvider))]
[Shared]
public sealed class ArrayPoolSqliteParameterSizeCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ArrayPoolSqliteParameterSizeAnalyzer.DiagnosticId];

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
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is InvocationExpressionSyntax)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add SqliteParameter.Size assignment",
                    ct => FixAddWithValueAsync(context.Document, node, ct),
                    nameof(ArrayPoolSqliteParameterSizeCodeFixProvider) + ".AddWithValue"),
                diagnostic);
        }
        else if (node is AssignmentExpressionSyntax)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add SqliteParameter.Size assignment",
                    ct => FixValueAssignmentAsync(context.Document, node, ct),
                    nameof(ArrayPoolSqliteParameterSizeCodeFixProvider) + ".Value"),
                diagnostic);
        }
    }

    private static async Task<Document> FixAddWithValueAsync(
        Document document,
        SyntaxNode invocationNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var containingStatement = invocationNode.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (containingStatement is null)
        {
            return document;
        }

        if (containingStatement.Parent is not BlockSyntax block)
        {
            return document;
        }

        var invocation = (InvocationExpressionSyntax)invocationNode;
        var isDiscard = containingStatement.Expression is AssignmentExpressionSyntax assign
                        && assign.Left is IdentifierNameSyntax discardId
                        && string.Equals(discardId.Identifier.ValueText, "_", StringComparison.Ordinal);

        var paramVarName = GenerateUniqueParameterName(block);

        var declarationStatement = isDiscard
            ? SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.IdentifierName("var"),
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.VariableDeclarator(paramVarName)
                                .WithInitializer(SyntaxFactory.EqualsValueClause(invocation)))))
                .WithLeadingTrivia(containingStatement.GetLeadingTrivia())
                .WithTrailingTrivia(containingStatement.GetTrailingTrivia())
            : (StatementSyntax)containingStatement;

        var sizeStatement = CreateSizeAssignmentStatement(
            SyntaxFactory.IdentifierName(paramVarName),
            containingStatement.GetLeadingTrivia());

        var stmtIndex = block.Statements.IndexOf(containingStatement);
        var newStatements = block.Statements
            .RemoveAt(stmtIndex)
            .Insert(stmtIndex, declarationStatement)
            .Insert(stmtIndex + 1, sizeStatement);

        var newBlock = block.WithStatements(newStatements);
        var newRoot = root.ReplaceNode(block, newBlock);

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> FixValueAssignmentAsync(
        Document document,
        SyntaxNode assignmentNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var assignment = (AssignmentExpressionSyntax)assignmentNode;
        if (assignment.Left is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        var containingStatement = assignmentNode.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (containingStatement is null)
        {
            return document;
        }

        if (containingStatement.Parent is not BlockSyntax block)
        {
            return document;
        }

        var sizeAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            memberAccess.Expression.WithoutTrivia(),
            SyntaxFactory.IdentifierName("Size"));

        var sizeStatement = CreateSizeAssignmentStatement(sizeAccess, containingStatement.GetLeadingTrivia());

        var stmtIndex = block.Statements.IndexOf(containingStatement);
        var newStatements = block.Statements.Insert(stmtIndex + 1, sizeStatement);
        var newBlock = block.WithStatements(newStatements);
        var newRoot = root.ReplaceNode(block, newBlock);

        return document.WithSyntaxRoot(newRoot);
    }

    private static ExpressionStatementSyntax CreateSizeAssignmentStatement(
        ExpressionSyntax target,
        SyntaxTriviaList leadingTrivia)
    {
        var sizeAccess = target is MemberAccessExpressionSyntax
            ? target
            : SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                target,
                SyntaxFactory.IdentifierName("Size"));

        return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    sizeAccess,
                    SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0))))
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(
                SyntaxFactory.Space,
                SyntaxFactory.Comment("// TODO: set to actual byte count"),
                SyntaxFactory.ElasticLineFeed);
    }

    private static string GenerateUniqueParameterName(BlockSyntax block)
    {
        var existingNames = block.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Select(v => v.Identifier.ValueText)
            .ToImmutableHashSet(StringComparer.Ordinal);

        if (!existingNames.Contains("sqliteParam"))
        {
            return "sqliteParam";
        }

        for (var i = 2; i < 100; i++)
        {
            var candidate = "sqliteParam" + i.ToString(CultureInfo.InvariantCulture);
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return "sqliteParam";
    }
}
