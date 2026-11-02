using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConfigureAwaitFalseCodeFixProvider))]
[Shared]
public sealed class ConfigureAwaitFalseCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ConfigureAwaitFalseE128Analyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove .ConfigureAwait(false)",
                    ct => RemoveConfigureAwaitAsync(context.Document, root, invocation, memberAccess, ct),
                    "RemoveConfigureAwaitFalse"),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    private static Task<Document> RemoveConfigureAwaitAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var receiver = memberAccess.Expression.WithTriviaFrom(invocation);
        var newRoot = root.ReplaceNode(invocation, receiver);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
