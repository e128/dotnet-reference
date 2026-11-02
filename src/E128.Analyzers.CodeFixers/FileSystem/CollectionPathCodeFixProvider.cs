using System.Collections.Immutable;
using System.Composition;
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
///     with <c lang="csharp">FileInfo</c> or <c lang="csharp">DirectoryInfo</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CollectionPathCodeFixProvider))]
[Shared]
public sealed class CollectionPathCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [CollectionPathAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(CollectionPathAnalyzer.SuggestedTypeKey, out var suggestedType)
            || suggestedType is null)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Change collection type argument to " + suggestedType,
                ct => ApplyFixAsync(context.Document, root, diagnostic, suggestedType, ct),
                nameof(CollectionPathCodeFixProvider)),
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

        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        var parameter = FindAncestor<ParameterSyntax>(token);
        if (parameter?.Type is null)
        {
            return Task.FromResult(document);
        }

        var newRoot = ReplaceStringTypeArgument(root, parameter.Type, suggestedType);
        newRoot = UsingDirectives.AddIfMissing(newRoot, "System.IO");
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
