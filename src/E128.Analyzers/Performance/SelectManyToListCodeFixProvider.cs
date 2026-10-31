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

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SelectManyToListCodeFixProvider))]
[Shared]
public sealed class SelectManyToListCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [SelectManyToListAnalyzer.DiagnosticId];

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

        var toListInvocation = root.FindNode(context.Diagnostics[0].Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();

        if (toListInvocation is null)
        {
            return;
        }

        if (toListInvocation.Expression is not MemberAccessExpressionSyntax toListAccess
            || toListAccess.Expression is not InvocationExpressionSyntax selectManyInvocation
            || selectManyInvocation.Expression is not MemberAccessExpressionSyntax selectManyAccess
            || selectManyInvocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        if (selectManyInvocation.ArgumentList.Arguments[0].Expression is not SimpleLambdaExpressionSyntax lambda
            || lambda.ExpressionBody is not ExpressionSyntax lambdaBody)
        {
            return;
        }

        // The rewrite replaces a whole statement. A call nested inside a larger expression cannot
        // be replaced without dropping the rest of that expression.
        if (FindReplacedStatement(toListInvocation) is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use foreach + AddRange",
                ct => ApplyFixAsync(context.Document, toListInvocation, selectManyAccess.Expression, lambda.Parameter, lambdaBody, ct),
                SelectManyToListAnalyzer.DiagnosticId),
            context.Diagnostics[0]);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        InvocationExpressionSyntax toListInvocation,
        ExpressionSyntax source,
        ParameterSyntax lambdaParam,
        ExpressionSyntax lambdaBody,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document;
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var typeInfo = semanticModel.GetTypeInfo(toListInvocation, cancellationToken);
        if (typeInfo.Type is not INamedTypeSymbol listType || !listType.IsGenericType)
        {
            return document;
        }

        if (FindReplacedStatement(toListInvocation) is not { } containingStatement)
        {
            return document;
        }

        var variableDeclarator = containingStatement.Declaration.Variables[0];
        var elementTypeName = listType.TypeArguments[0].ToMinimalDisplayString(semanticModel, toListInvocation.SpanStart);
        var variableName = variableDeclarator.Identifier.Text;

        var newStatements = BuildReplacementStatements(
            variableName, elementTypeName, lambdaParam.Identifier.Text, source, lambdaBody, containingStatement.GetLeadingTrivia());

        var newRoot = root.ReplaceNode(containingStatement, newStatements);
        return document.WithSyntaxRoot(UsingDirectives.AddIfMissing(newRoot, "System.Collections.Generic"));
    }

    // Only a declaration whose whole initializer is the ToList call can be replaced. Anything
    // wider loses the expression around the call.
    private static LocalDeclarationStatementSyntax? FindReplacedStatement(InvocationExpressionSyntax toListInvocation)
    {
        var statement = toListInvocation.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();

        return statement is not null
               && statement.Declaration.Variables.Count == 1
               && statement.Declaration.Variables[0].Initializer?.Value == toListInvocation
            ? statement
            : null;
    }

    private static SyntaxNode[] BuildReplacementStatements(
        string variableName,
        string elementTypeName,
        string iteratorName,
        ExpressionSyntax source,
        ExpressionSyntax lambdaBody,
        SyntaxTriviaList leadingTrivia)
    {
        var declarationStatement = SyntaxFactory.ParseStatement(
                $"var {variableName} = new List<{elementTypeName}>();")
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var foreachStatement = SyntaxFactory.ForEachStatement(
                SyntaxFactory.IdentifierName("var"),
                iteratorName,
                source.WithoutTrivia(),
                SyntaxFactory.Block(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName(variableName),
                                SyntaxFactory.IdentifierName("AddRange")),
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(lambdaBody.WithoutTrivia())))))))
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed)
            .WithAdditionalAnnotations(Formatter.Annotation);

        return [declarationStatement, foreachStatement];
    }
}
