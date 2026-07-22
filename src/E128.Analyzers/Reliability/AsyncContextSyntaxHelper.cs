using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Shared ancestor walk used by every "blocking-call-with-async-sibling" analyzer
///     (E128092, E128093, ...) to decide whether an invocation already sits inside an
///     async method/local-function/lambda -- CA1849/VSTHRD103 already cover that case.
/// </summary>
internal static class AsyncContextSyntaxHelper
{
    public static bool IsInsideAsyncContext(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            bool? isAsync = ancestor switch
            {
                MethodDeclarationSyntax m => m.Modifiers.Any(SyntaxKind.AsyncKeyword),
                LocalFunctionStatementSyntax lf => lf.Modifiers.Any(SyntaxKind.AsyncKeyword),
                ConstructorDeclarationSyntax => false,
                ParenthesizedLambdaExpressionSyntax lambda => lambda.AsyncKeyword != default,
                SimpleLambdaExpressionSyntax lambda => lambda.AsyncKeyword != default,
                AnonymousMethodExpressionSyntax anon => anon.AsyncKeyword != default,
                _ => null
            };

            if (isAsync is { } found)
            {
                return found;
            }
        }

        return false;
    }
}
