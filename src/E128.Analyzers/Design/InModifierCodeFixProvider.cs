using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InModifierCodeFixProvider))]
[Shared]
public sealed class InModifierCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
    [
        InCancellationTokenE128Analyzer.DiagnosticId,
        Reliability.InMutableStructE128Analyzer.DiagnosticId,
        InRefStructE128Analyzer.DiagnosticId,
    ];

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
            if (node is not ParameterSyntax parameter)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove 'in' modifier",
                    createChangedDocument: ct => RemoveInModifierAsync(context.Document, root, parameter, ct),
                    equivalenceKey: "RemoveInModifier"),
                diagnostic);
        }
    }

    private static Task<Document> RemoveInModifierAsync(
        Document document,
        SyntaxNode root,
        ParameterSyntax parameter,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var newParameter = InModifierHelper.RemoveInModifier(parameter);
        var newRoot = root.ReplaceNode(parameter, newParameter);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
