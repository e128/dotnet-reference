using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InModifierCodeFixProvider))]
[Shared]
public sealed class InModifierCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
    [
        InCancellationTokenE128Analyzer.DiagnosticId,
        InMutableStructE128Analyzer.DiagnosticId,
        InRefStructE128Analyzer.DiagnosticId
    ];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is not ParameterSyntax parameter)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove 'in' modifier",
                    ct => RemoveInModifierAsync(context.Document, root, parameter, ct),
                    "RemoveInModifier"),
                diagnostic);
        }

        return Task.CompletedTask;
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
