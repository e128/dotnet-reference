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
///     Code fix for E128052: replaces mutable collection types (List&lt;T&gt;, HashSet&lt;T&gt;, etc.)
///     with their immutable interface counterparts (IReadOnlyList&lt;T&gt;, IReadOnlySet&lt;T&gt;, etc.)
///     on public/internal API surfaces.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MutableCollectionExposureCodeFixProvider))]
[Shared]
public sealed class MutableCollectionExposureCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [MutableCollectionExposureAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(MutableCollectionExposureAnalyzer.SuggestedTypeKey, out var suggestedType)
            || suggestedType is null)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Change return type to " + suggestedType,
                ct => ApplyFixAsync(context.Document, root, diagnostic, suggestedType, ct),
                nameof(MutableCollectionExposureCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
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
            var newReturnType = KeepNullability(suggestedType, method.ReturnType);
            newRoot = root.ReplaceNode(method, method.WithReturnType(newReturnType));
        }
        else if (node is PropertyDeclarationSyntax property)
        {
            var newPropertyType = KeepNullability(suggestedType, property.Type);
            newRoot = root.ReplaceNode(property, property.WithType(newPropertyType));
        }

        if (newRoot is null)
        {
            return Task.FromResult(document);
        }

        newRoot = UsingDirectives.AddIfMissing(newRoot, "System.Collections.Generic");
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    /// <summary>
    ///     The suggested type comes from the analyzer without a null annotation. A nullable member
    ///     must stay nullable, or the fix turns a working null return into a warning.
    /// </summary>
    private static TypeSyntax KeepNullability(string suggestedType, TypeSyntax originalType)
    {
        var newType = SyntaxFactory.ParseTypeName(suggestedType);

        return (originalType is NullableTypeSyntax
                ? SyntaxFactory.NullableType(newType)
                : newType)
            .WithTriviaFrom(originalType);
    }
}
