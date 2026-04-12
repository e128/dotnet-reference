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
/// Code fix for E128042: wraps the Convert.ToInt32/ToInt64(cmd.ExecuteScalar()) call
/// in a null-check pattern that extracts the scalar result into a local variable,
/// checks for null, then converts.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExecuteScalarNullGuardCodeFixProvider))]
[Shared]
public sealed class ExecuteScalarNullGuardCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ExecuteScalarNullGuardAnalyzer.DiagnosticId];

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

        if (node is not InvocationExpressionSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add null guard around ExecuteScalar",
                createChangedDocument: ct => ApplyFixAsync(context.Document, node, ct),
                equivalenceKey: nameof(ExecuteScalarNullGuardCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode diagnosticNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || diagnosticNode is not InvocationExpressionSyntax convertInvocation)
        {
            return document;
        }

        if (!TryExtractParts(convertInvocation, out var innerExpr, out var convertMethodName))
        {
            return document;
        }

        var nullGuard = BuildNullGuardExpression(convertMethodName);
        var resultDecl = BuildResultDeclaration(innerExpr);
        return InsertFixIntoTree(document, root, convertInvocation, nullGuard, resultDecl);
    }

    private static bool TryExtractParts(
        InvocationExpressionSyntax convertInvocation,
        out ExpressionSyntax innerExpr,
        out string convertMethodName)
    {
        innerExpr = null!;
        convertMethodName = string.Empty;

        if (!convertInvocation.ArgumentList.Arguments.Any())
        {
            return false;
        }

        innerExpr = convertInvocation.ArgumentList.Arguments[0].Expression;

        if (convertInvocation.Expression is not MemberAccessExpressionSyntax convertAccess)
        {
            return false;
        }

        convertMethodName = convertAccess.Name.Identifier.ValueText;
        return true;
    }

    private static ConditionalExpressionSyntax BuildNullGuardExpression(string convertMethodName)
    {
        return SyntaxFactory.ConditionalExpression(
            SyntaxFactory.IsPatternExpression(
                SyntaxFactory.IdentifierName("result"),
                SyntaxFactory.ConstantPattern(
                    SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))),
            SyntaxFactory.LiteralExpression(
                SyntaxKind.NumericLiteralExpression,
                SyntaxFactory.Literal(0)),
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Convert"),
                    SyntaxFactory.IdentifierName(convertMethodName)),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName("result"))))));
    }

    private static LocalDeclarationStatementSyntax BuildResultDeclaration(ExpressionSyntax innerExpr)
    {
        return SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.IdentifierName("var"),
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator("result")
                        .WithInitializer(
                            SyntaxFactory.EqualsValueClause(innerExpr.WithoutTrivia())))))
            .WithTrailingTrivia(SyntaxFactory.LineFeed);
    }

    private static Document InsertFixIntoTree(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax convertInvocation,
        ConditionalExpressionSyntax nullGuard,
        LocalDeclarationStatementSyntax resultDecl)
    {
        var newRoot = root.ReplaceNode(convertInvocation, nullGuard.WithTriviaFrom(convertInvocation));

        var containingStatement = convertInvocation.FirstAncestorOrSelf<StatementSyntax>();
        if (containingStatement is null)
        {
            return document.WithSyntaxRoot(newRoot);
        }

        var updatedStatement = newRoot.FindNode(containingStatement.Span).FirstAncestorOrSelf<StatementSyntax>();
        if (updatedStatement?.Parent is not BlockSyntax block)
        {
            return document.WithSyntaxRoot(newRoot);
        }

        var index = block.Statements.IndexOf(updatedStatement);
        if (index < 0)
        {
            return document.WithSyntaxRoot(newRoot);
        }

        var leadingTrivia = updatedStatement.GetLeadingTrivia();
        var newStatements = block.Statements.Insert(index,
            resultDecl.WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(SyntaxFactory.LineFeed));
        var newBlock = block.WithStatements(newStatements);
        newRoot = newRoot.ReplaceNode(block, newBlock);

        return document.WithSyntaxRoot(newRoot);
    }
}
