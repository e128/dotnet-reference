using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

/// <summary>
///     Rewrites a DI registration call to the two-type-argument overload. The design and reliability
///     fixes share this edit.
/// </summary>
internal static class DiRegistrationFix
{
    /// <summary>
    ///     Rewrites the call to name the interface and the concrete type. A null
    ///     <paramref name="methodName" /> keeps the existing method name.
    /// </summary>
    internal static async Task<Document> RewriteToGenericAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ITypeSymbol concreteType,
        INamedTypeSymbol targetInterface,
        SyntaxToken? methodName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document;
        }

        // The minimal form qualifies a name only when it is out of scope, and it escapes a keyword
        // identifier. It also matches the action title, so the preview shows the text that lands.
        var newTypeArgs = SyntaxFactory.TypeArgumentList(
            SyntaxFactory.SeparatedList(
                [
                    SyntaxFactory.ParseTypeName(
                        targetInterface.ToMinimalDisplayString(semanticModel, invocation.SpanStart)),
                    SyntaxFactory.ParseTypeName(
                        concreteType.ToMinimalDisplayString(semanticModel, invocation.SpanStart))
                ]));

        var newName = SyntaxFactory.GenericName(methodName ?? memberAccess.Name.Identifier)
            .WithTypeArgumentList(newTypeArgs);

        var newInvocation = invocation
            .WithExpression(memberAccess.WithName(newName))
            .WithArgumentList(SyntaxFactory.ArgumentList());

        var newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
