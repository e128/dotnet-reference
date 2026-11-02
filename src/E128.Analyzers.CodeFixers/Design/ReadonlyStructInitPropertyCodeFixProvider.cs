using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128072: adds <c lang="csharp">init</c> accessor to get-only properties in readonly structs.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ReadonlyStructInitPropertyCodeFixProvider))]
[Shared]
public sealed class ReadonlyStructInitPropertyCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ReadonlyStructInitPropertyAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var property = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        if (property is null)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add init accessor",
                ct => AddInitAccessorAsync(context.Document, property, ct),
                nameof(ReadonlyStructInitPropertyCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Document> AddInitAccessorAsync(
        Document document,
        PropertyDeclarationSyntax property,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || property.AccessorList is null)
        {
            return document;
        }

        var initAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(SyntaxFactory.Space)
            .WithTrailingTrivia(SyntaxFactory.Space);

        var newAccessorList = property.AccessorList.AddAccessors(initAccessor);
        var newProperty = property.WithAccessorList(newAccessorList);

        var newRoot = root.ReplaceNode(property, newProperty);
        return document.WithSyntaxRoot(newRoot);
    }
}
