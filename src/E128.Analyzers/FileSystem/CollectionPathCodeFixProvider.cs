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

namespace E128.Analyzers.FileSystem;

/// <summary>
///     Code fix for E128053: replaces the <see langword="string" /> type argument in collection parameters
///     with <c>FileInfo</c> or <c>DirectoryInfo</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CollectionPathCodeFixProvider))]
[Shared]
public sealed class CollectionPathCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [CollectionPathAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
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
        if (!diagnostic.Properties.TryGetValue(CollectionPathAnalyzer.SuggestedTypeKey, out var suggestedType)
            || suggestedType is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Change collection type argument to " + suggestedType,
                ct => ApplyFixAsync(context.Document, root, diagnostic, suggestedType, ct),
                nameof(CollectionPathCodeFixProvider)),
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

        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        var parameter = FindAncestor<ParameterSyntax>(token);
        if (parameter?.Type is null)
        {
            return Task.FromResult(document);
        }

        var newRoot = ReplaceStringTypeArgument(root, parameter.Type, suggestedType);
        newRoot = EnsureUsingDirective(newRoot);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static SyntaxNode ReplaceStringTypeArgument(SyntaxNode root, TypeSyntax paramType, string suggestedType)
    {
        var genericName = paramType switch
        {
            GenericNameSyntax direct => direct,
            QualifiedNameSyntax qualified when qualified.Right is GenericNameSyntax nested => nested,
            _ => null
        };

        if (genericName is null)
        {
            return root;
        }

        var typeArgs = genericName.TypeArgumentList.Arguments;
        if (typeArgs.Count != 1 || typeArgs[0] is not PredefinedTypeSyntax)
        {
            return root;
        }

        var newTypeArg = SyntaxFactory.IdentifierName(suggestedType);
        var newTypeArgList = genericName.TypeArgumentList.WithArguments(
            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(newTypeArg));
        var newGenericName = genericName.WithTypeArgumentList(newTypeArgList);

        return root.ReplaceNode(genericName, newGenericName);
    }

    private static SyntaxNode EnsureUsingDirective(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        var hasSystemIo = compilationUnit.Usings.Any(u =>
            string.Equals(u.Name?.ToString(), "System.IO", StringComparison.Ordinal));

        if (hasSystemIo)
        {
            return root;
        }

        var usingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.QualifiedName(
                    SyntaxFactory.IdentifierName("System"),
                    SyntaxFactory.IdentifierName("IO")))
            .NormalizeWhitespace()
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        return compilationUnit.AddUsings(usingDirective);
    }

    private static T? FindAncestor<T>(SyntaxToken token)
        where T : SyntaxNode
    {
        var node = token.Parent;
        while (node is not null)
        {
            if (node is T result)
            {
                return result;
            }

            node = node.Parent;
        }

        return null;
    }
}
