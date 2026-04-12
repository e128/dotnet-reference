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
public sealed class ConfigureAwaitFalseCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ConfigureAwaitFalseE128Analyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

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
                    title: "Remove .ConfigureAwait(false)",
                    createChangedDocument: ct => RemoveConfigureAwaitAsync(context.Document, root, invocation, memberAccess, ct),
                    equivalenceKey: "RemoveConfigureAwaitFalse"),
                diagnostic);
        }
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
