using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OceWhenTokenFilterCodeFixProvider))]
[Shared]
public sealed class OceWhenTokenFilterCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [OceWhenTokenFilterAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var catchClause = node.FirstAncestorOrSelf<CatchClauseSyntax>();
        if (catchClause?.Filter is null)
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Remove negated token-state filter", nameof(OceWhenTokenFilterCodeFixProvider), ct => RemoveFilterAsync(context.Document, root, catchClause, ct));

        return Task.CompletedTask;
    }

    private static Task<Document> RemoveFilterAsync(
        Document document,
        SyntaxNode root,
        CatchClauseSyntax catchClause,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (catchClause.Declaration is not { } declaration || catchClause.Filter is not { } filter)
        {
            return Task.FromResult(document);
        }

        var newCatchClause = catchClause
            .WithFilter(null)
            .WithDeclaration(declaration.WithTrailingTrivia(filter.GetTrailingTrivia()));

        var newRoot = root.ReplaceNode(catchClause, newCatchClause);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
