using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

/// <summary>
///     Rewrites a returned collection expression to its read-only view. The list and dictionary
///     analyzers share this edit.
/// </summary>
internal static class ReadOnlyCollectionFix
{
    /// <summary>
    ///     Appends <c lang="csharp">.AsReadOnly()</c> to the expression and keeps its trivia.
    /// </summary>
    internal static async Task<Document> AppendAsReadOnlyAsync(
        Document document,
        ExpressionSyntax expression,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var asReadOnly = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    expression.WithoutTrivia(),
                    SyntaxFactory.IdentifierName("AsReadOnly")))
            .WithTriviaFrom(expression);

        var newRoot = root.ReplaceNode(expression, asReadOnly);
        return document.WithSyntaxRoot(newRoot);
    }
}
