using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128049: removes the <c>[DynamicallyAccessedMembers]</c> attribute.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DynamicallyAccessedMembersGuardCodeFixProvider))]
[Shared]
public sealed class DynamicallyAccessedMembersGuardCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DynamicallyAccessedMembersGuardAnalyzer.DiagnosticId];

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
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var attribute = node.FirstAncestorOrSelf<AttributeSyntax>();
        if (attribute is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove [DynamicallyAccessedMembers] attribute",
                ct => RemoveAttributeAsync(context.Document, attribute, ct),
                nameof(DynamicallyAccessedMembersGuardCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> RemoveAttributeAsync(
        Document document,
        AttributeSyntax attribute,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (attribute.Parent is not AttributeListSyntax attributeList)
        {
            return document;
        }

        SyntaxNode newRoot;

        if (attributeList.Attributes.Count == 1)
        {
            // Only one attribute in the list — remove the entire attribute list.
            newRoot = root.RemoveNode(attributeList, SyntaxRemoveOptions.KeepNoTrivia)!;
        }
        else
        {
            // Multiple attributes in the list — remove just this one.
            var newList = attributeList.RemoveNode(attribute, SyntaxRemoveOptions.KeepNoTrivia);
            newRoot = newList is null ? root : root.ReplaceNode(attributeList, newList);
        }

        return document.WithSyntaxRoot(newRoot);
    }
}
