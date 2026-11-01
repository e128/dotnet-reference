using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Unsafe;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnsafeAddArrayDataReferenceCodeFixProvider))]
[Shared]
public sealed class UnsafeAddArrayDataReferenceCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnsafeAddArrayDataReferenceAnalyzer.DiagnosticId];

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

        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var access = UnsafeAddArrayDataReferenceAnalyzer.TryGetIndexedAccess(invocation);
        if (access is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Index the array directly",
                _ => Task.FromResult(ApplyFix(context.Document, root, invocation, access.Value)),
                nameof(UnsafeAddArrayDataReferenceCodeFixProvider)),
            diagnostic);
    }

    private static Document ApplyFix(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        (ExpressionSyntax Array, ExpressionSyntax Index) access)
    {
        var replacement = FixSyntax.IndexInto(access.Array, access.Index).WithTriviaFrom(invocation);
        return document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
    }
}
