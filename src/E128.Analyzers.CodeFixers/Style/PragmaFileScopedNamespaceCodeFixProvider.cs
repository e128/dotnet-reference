using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Style;

/// <summary>
///     Code fix for E128094: relocates a <c lang="csharp">#pragma warning disable</c> directive from
///     above a file-scoped namespace declaration to the first token after it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PragmaFileScopedNamespaceCodeFixProvider))]
[Shared]
public sealed class PragmaFileScopedNamespaceCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [PragmaFileScopedNamespaceAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
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
        if (root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true)
            is not PragmaWarningDirectiveTriviaSyntax pragma)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Move #pragma below the namespace declaration",
                ct => MovePragmaAsync(context.Document, pragma, ct),
                nameof(PragmaFileScopedNamespaceCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> MovePragmaAsync(
        Document document,
        PragmaWarningDirectiveTriviaSyntax pragma,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return document;
        }

        var namespaceDeclaration = compilationUnit.DescendantNodes()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        if (namespaceDeclaration is null)
        {
            return document;
        }

        var targetToken = namespaceDeclaration.SemicolonToken.GetNextToken();
        if (targetToken.IsKind(SyntaxKind.None) || targetToken.IsKind(SyntaxKind.EndOfFileToken))
        {
            return document;
        }

        var pragmaTrivia = pragma.ParentTrivia;
        var owningToken = pragmaTrivia.Token;

        var pragmaIndex = owningToken.LeadingTrivia.IndexOf(pragmaTrivia);
        if (pragmaIndex < 0)
        {
            return document;
        }

        var remaining = owningToken.LeadingTrivia.RemoveAt(pragmaIndex);
        if (pragmaIndex < remaining.Count && remaining[pragmaIndex].IsKind(SyntaxKind.EndOfLineTrivia))
        {
            remaining = remaining.RemoveAt(pragmaIndex);
        }

        var newOwningToken = owningToken.WithLeadingTrivia(remaining);
        var newTargetToken = targetToken.WithLeadingTrivia(
            SyntaxFactory.TriviaList(pragmaTrivia).AddRange(targetToken.LeadingTrivia));

        var newRoot = compilationUnit.ReplaceTokens(
            [owningToken, targetToken],
            (old, _) => old == owningToken ? newOwningToken : newTargetToken);

        return document.WithSyntaxRoot(newRoot);
    }
}
