using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Testing;

/// <summary>
///     Code fix for E128071: adds <c>[Trait("Category", "CI")]</c> to the test method.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingTraitCategoryCodeFixProvider))]
[Shared]
public sealed class MissingTraitCategoryCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [MissingTraitCategoryAnalyzer.DiagnosticId];

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

        var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add [Trait(\"Category\", \"CI\")]",
                ct => AddTraitAttributeAsync(context.Document, method, ct),
                nameof(MissingTraitCategoryCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddTraitAttributeAsync(
        Document document,
        MethodDeclarationSyntax method,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var traitAttribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("Trait"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SeparatedList(
                    [
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal("Category"))),
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal("CI")))
                    ])));

        var lastAttrList = method.AttributeLists.Last();
        var leadingTrivia = lastAttrList.GetLeadingTrivia();

        var traitAttrList = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(traitAttribute))
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        var newMethod = method.WithAttributeLists(method.AttributeLists.Add(traitAttrList));
        var newRoot = root.ReplaceNode(method, newMethod);
        return document.WithSyntaxRoot(newRoot);
    }
}
