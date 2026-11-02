using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128033: replaces the <c lang="csharp">init</c> accessor keyword with <c lang="csharp">set</c>
///     so the configuration binder can populate the property at runtime.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OptionsBindInitCodeFixProvider))]
[Shared]
public sealed class OptionsBindInitCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [OptionsBindInitAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        // FindNode may return the accessor directly (when the diagnostic span covers the full accessor)
        // or a child node. Fall back to finding the token when the span targets the 'init' keyword.
        var accessor = node as AccessorDeclarationSyntax
                       ?? root.FindToken(diagnostic.Location.SourceSpan.Start).Parent as AccessorDeclarationSyntax;

        if (accessor is null)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Change 'init' to 'set'",
                ct => ChangeInitToSetAsync(context.Document, accessor, ct),
                nameof(OptionsBindInitCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Document> ChangeInitToSetAsync(
        Document document,
        AccessorDeclarationSyntax accessor,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var setKeyword = SyntaxFactory.Token(SyntaxKind.SetKeyword)
            .WithLeadingTrivia(accessor.Keyword.LeadingTrivia)
            .WithTrailingTrivia(accessor.Keyword.TrailingTrivia);

        var newAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
            .WithModifiers(accessor.Modifiers)
            .WithKeyword(setKeyword)
            .WithBody(accessor.Body)
            .WithExpressionBody(accessor.ExpressionBody)
            .WithSemicolonToken(accessor.SemicolonToken);

        var newRoot = root.ReplaceNode(accessor, newAccessor);
        return document.WithSyntaxRoot(newRoot);
    }
}
