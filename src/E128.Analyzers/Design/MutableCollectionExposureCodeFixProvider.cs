using System;
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

namespace E128.Analyzers.Design;

/// <summary>
/// Code fix for E128052: replaces mutable collection types (List&lt;T&gt;, HashSet&lt;T&gt;, etc.)
/// with their immutable interface counterparts (IReadOnlyList&lt;T&gt;, IReadOnlySet&lt;T&gt;, etc.)
/// on public/internal API surfaces.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MutableCollectionExposureCodeFixProvider))]
[Shared]
public sealed class MutableCollectionExposureCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [MutableCollectionExposureAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(MutableCollectionExposureAnalyzer.SuggestedTypeKey, out var suggestedType)
            || suggestedType is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Change return type to " + suggestedType,
                createChangedDocument: ct => ApplyFixAsync(context.Document, root, diagnostic, suggestedType, ct),
                equivalenceKey: nameof(MutableCollectionExposureCodeFixProvider)),
            diagnostic);
    }

    private static Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode root,
        Diagnostic diagnostic,
        string suggestedType,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var node = root.FindNode(diagnostic.Location.SourceSpan);

        SyntaxNode? newRoot = null;

        if (node is MethodDeclarationSyntax method)
        {
            var newReturnType = SyntaxFactory.ParseTypeName(suggestedType)
                .WithTriviaFrom(method.ReturnType);
            newRoot = root.ReplaceNode(method, method.WithReturnType(newReturnType));
        }
        else if (node is PropertyDeclarationSyntax property)
        {
            var newPropertyType = SyntaxFactory.ParseTypeName(suggestedType)
                .WithTriviaFrom(property.Type);
            newRoot = root.ReplaceNode(property, property.WithType(newPropertyType));
        }

        if (newRoot is null)
        {
            return Task.FromResult(document);
        }

        newRoot = EnsureUsingCollectionsGeneric(newRoot);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static SyntaxNode EnsureUsingCollectionsGeneric(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        if (compilationUnit.Usings.Any(u =>
            string.Equals(u.Name?.ToString(), "System.Collections.Generic", StringComparison.Ordinal)))
        {
            return root;
        }

        var usingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.QualifiedName(
                    SyntaxFactory.QualifiedName(
                        SyntaxFactory.IdentifierName("System"),
                        SyntaxFactory.IdentifierName("Collections")),
                    SyntaxFactory.IdentifierName("Generic")))
            .NormalizeWhitespace()
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        return compilationUnit.AddUsings(usingDirective);
    }
}
