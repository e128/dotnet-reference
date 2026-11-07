using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ArrayPoolSqliteParameterSizeCodeFixProvider))]
[Shared]
public sealed class ArrayPoolSqliteParameterSizeCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ArrayPoolSqliteParameterSizeAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        // The fix needs the length the code requested from the pool. Without it, no fix is offered.
        if (node is InvocationExpressionSyntax addWithValue
            && TryGetRequestedLength(node, GetAddWithValueArgument(addWithValue)) is not null)
        {
            CodeFixRegistration.Register(context, diagnostic, "Add SqliteParameter.Size assignment", nameof(ArrayPoolSqliteParameterSizeCodeFixProvider) + ".AddWithValue", ct => FixAddWithValueAsync(context.Document, node, ct));
        }
        else if (node is AssignmentExpressionSyntax valueAssignment
                 && TryGetRequestedLength(node, valueAssignment.Right) is not null)
        {
            CodeFixRegistration.Register(context, diagnostic, "Add SqliteParameter.Size assignment", nameof(ArrayPoolSqliteParameterSizeCodeFixProvider) + ".Value", ct => FixValueAssignmentAsync(context.Document, node, ct));
        }

        return Task.CompletedTask;
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

        var length = TryGetRequestedLength(invocation, GetAddWithValueArgument(invocation));
        if (length is null)
        {
            return document;
        }

        var paramVarName = GenerateUniqueParameterName(block);

        var declaration = BuildParameterDeclaration(
            paramVarName, invocation, containingStatement.GetLeadingTrivia(), containingStatement.GetTrailingTrivia());

        var sizeStatement = CreateSizeAssignmentStatement(
            SyntaxFactory.IdentifierName(paramVarName),
            length,
            containingStatement.GetLeadingTrivia());

        var replacement = new List<StatementSyntax> { declaration, sizeStatement };

        // A statement that only performs the call keeps no second copy of it. Any other statement
        // still reads the parameter, so the call moves into the declaration and the statement uses
        // the new name.
        if (!isDiscard
            && containingStatement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax })
        {
            replacement.Add(containingStatement.ReplaceNode(
                invocation, SyntaxFactory.IdentifierName(paramVarName)));
        }

        var stmtIndex = block.Statements.IndexOf(containingStatement);
        var newStatements = block.Statements.RemoveAt(stmtIndex).InsertRange(stmtIndex, replacement);

        var newBlock = block.WithStatements(newStatements);
        var newRoot = root.ReplaceNode(block, newBlock);

        return document.WithSyntaxRoot(newRoot);
    }

    private static LocalDeclarationStatementSyntax BuildParameterDeclaration(
        string paramVarName,
        InvocationExpressionSyntax invocation,
        SyntaxTriviaList leadingTrivia,
        SyntaxTriviaList trailingTrivia)
    {
        return SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.IdentifierName("var"),
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(paramVarName)
                            .WithInitializer(SyntaxFactory.EqualsValueClause(invocation.WithoutTrivia())))))
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(trailingTrivia);
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

        var length = TryGetRequestedLength(assignment, assignment.Right);
        if (length is null)
        {
            return document;
        }

        var sizeStatement = CreateSizeAssignmentStatement(sizeAccess, length, containingStatement.GetLeadingTrivia());

        var stmtIndex = block.Statements.IndexOf(containingStatement);
        var newStatements = block.Statements.Insert(stmtIndex + 1, sizeStatement);
        var newBlock = block.WithStatements(newStatements);
        var newRoot = root.ReplaceNode(block, newBlock);

        return document.WithSyntaxRoot(newRoot);
    }

    private static ExpressionStatementSyntax CreateSizeAssignmentStatement(
        ExpressionSyntax target,
        ExpressionSyntax length,
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
                    length.WithoutTrivia()))
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);
    }

    private static ExpressionSyntax? GetAddWithValueArgument(InvocationExpressionSyntax invocation)
    {
        return invocation.ArgumentList.Arguments.Count == 2
            ? invocation.ArgumentList.Arguments[1].Expression
            : null;
    }

    /// <summary>
    ///     Returns the length passed to `ArrayPool.Rent` for the buffer that <paramref name="value"/>
    ///     names, or null when the code does not show it. Only a literal or a plain identifier qualifies,
    ///     so the copied expression has no side effect and means the same at the new statement.
    /// </summary>
    private static ExpressionSyntax? TryGetRequestedLength(SyntaxNode scope, ExpressionSyntax? value)
    {
        if (value is null)
        {
            return null;
        }

        var rent = value as InvocationExpressionSyntax;
        if (rent is null && value is IdentifierNameSyntax bufferName)
        {
            var member = scope.FirstAncestorOrSelf<MemberDeclarationSyntax>();
            var name = bufferName.Identifier.ValueText;
            var declarators = member?.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => string.Equals(v.Identifier.ValueText, name, StringComparison.Ordinal))
                .ToList();

            // Decline when the name is ambiguous. Two declarators of one name can sit in different scopes,
            // and a reassigned buffer can hold a different length at the call.
            if (declarators is not { Count: 1 }
                || member is null
                || member.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(a =>
                    a.Left is IdentifierNameSyntax left
                    && string.Equals(left.Identifier.ValueText, name, StringComparison.Ordinal)))
            {
                return null;
            }

            rent = declarators[0].Initializer?.Value as InvocationExpressionSyntax;
        }

        if (rent is null
            || rent.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Rent" }
            || rent.ArgumentList.Arguments.Count != 1)
        {
            return null;
        }

        var length = rent.ArgumentList.Arguments[0].Expression;
        return length is LiteralExpressionSyntax or IdentifierNameSyntax ? length : null;
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
