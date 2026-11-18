using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

/// <summary>
///     Code fix for E128229: changes the next delegate to
///     <c lang="csharp">RequestDelegate</c> and passes the context to every call.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MiddlewareNextDelegateCodeFixProvider))]
[Shared]
public sealed class MiddlewareNextDelegateCodeFixProvider : E128CodeFixProviderBase
{
    private const string HttpNamespace = "Microsoft.AspNetCore.Http";

    private const int MiddlewareParameterCount = 2;

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [MiddlewareNextDelegateAnalyzer.DiagnosticId];

    /// <inheritdoc />
    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var lambda = root.FindNode(context.Diagnostics[0].Location.SourceSpan, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .OfType<ParenthesizedLambdaExpressionSyntax>()
            .FirstOrDefault();

        if (lambda is not { ParameterList.Parameters.Count: MiddlewareParameterCount }
            || lambda.ParameterList.Parameters[1].Type is null)
        {
            return Task.CompletedTask;
        }

        var contextName = lambda.ParameterList.Parameters[0].Identifier.ValueText;
        var nextName = lambda.ParameterList.Parameters[1].Identifier.ValueText;

        if (string.IsNullOrEmpty(contextName)
            || string.IsNullOrEmpty(nextName)
            || string.Equals(contextName, "_", StringComparison.Ordinal)
            || !MiddlewareNextDelegateAnalyzer.CallsNextWithNoArgument(lambda.Body, contextName, nextName))
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(
            context,
            context.Diagnostics[0],
            "Use the RequestDelegate overload",
            MiddlewareNextDelegateAnalyzer.DiagnosticId,
            ct => ApplyFixAsync(context.Document, lambda, contextName, nextName, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        ParenthesizedLambdaExpressionSyntax lambda,
        string contextName,
        string nextName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var replacement = new NextDelegateRewriter(contextName, nextName).Visit(lambda);

        if (replacement is not ParenthesizedLambdaExpressionSyntax rewritten)
        {
            return document;
        }

        var newRoot = UsingDirectives.AddIfMissing(
            root.ReplaceNode(lambda, rewritten.WithTriviaFrom(lambda)),
            HttpNamespace);

        return document.WithSyntaxRoot(newRoot);
    }

    private sealed class NextDelegateRewriter : CSharpSyntaxRewriter
    {
        private readonly string _contextName;
        private readonly string _nextName;

        public NextDelegateRewriter(string contextName, string nextName)
        {
            _contextName = contextName;
            _nextName = nextName;
        }

        /// <summary>
        ///     Writes <c lang="csharp">RequestDelegate</c> in the next parameter's type position.
        /// </summary>
        public override SyntaxNode? VisitParameter(ParameterSyntax node)
        {
            if (string.Equals(node.Identifier.ValueText, _nextName, StringComparison.Ordinal) && node.Type is { } type)
            {
                return node.WithType(
                    SyntaxFactory.IdentifierName("RequestDelegate").WithTriviaFrom(type));
            }

            return base.VisitParameter(node);
        }

        /// <summary>
        ///     Passes the context to a zero-argument next call.
        /// </summary>
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax?)base.VisitInvocationExpression(node);

            if (visited is { Expression: IdentifierNameSyntax { Identifier.ValueText: var name } }
                && string.Equals(name, _nextName, StringComparison.Ordinal))
            {
                return visited.WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName(_contextName)))));
            }

            return visited;
        }
    }
}
