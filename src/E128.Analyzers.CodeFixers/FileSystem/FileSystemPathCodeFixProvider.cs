using System;
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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FileSystemPathCodeFixProvider))]
[Shared]
public sealed class FileSystemPathCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [FileSystemPathAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(FileSystemPathAnalyzer.SuggestedTypeKey, out var suggested)
            || suggested is null)
        {
            return Task.CompletedTask;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);

        // For parameter diagnostics from use-site analysis, the method body depends on the
        // parameter being a string. Changing the type would break compilation, so only
        // offer fixes for ambiguous (name-pattern) diagnostics where the body is empty/absent.
        var isAmbiguous = string.Equals(suggested, FileSystemPathAnalyzer.SuggestedAmbiguous, StringComparison.Ordinal);
        if (node is ParameterSyntax && !isAmbiguous)
        {
            return Task.CompletedTask;
        }

        if (isAmbiguous)
        {
            RegisterFix(context, diagnostic, node, FileSystemPathAnalyzer.SuggestedFileInfo);
            RegisterFix(context, diagnostic, node, FileSystemPathAnalyzer.SuggestedDirectoryInfo);
        }
        else
        {
            RegisterFix(context, diagnostic, node, suggested);
        }

        return Task.CompletedTask;
    }

    private static void RegisterFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        SyntaxNode node,
        string typeName)
    {
        // Parameter case: diagnostic location is on the identifier token → FindNode returns ParameterSyntax.
        // Option/Argument case: diagnostic location is on the PredefinedTypeSyntax (string keyword).
        if (node is not ParameterSyntax and not PredefinedTypeSyntax)
        {
            return;
        }

        var title = $"Change type to {typeName}";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                ct => ApplyFixAsync(context.Document, node, typeName, ct),
                $"{nameof(FileSystemPathCodeFixProvider)}_{typeName}"),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode node,
        string typeName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var replacement = SyntaxFactory.IdentifierName(typeName);
        SyntaxNode newRoot;

        if (node is ParameterSyntax param && param.Type is not null)
        {
            var newParam = param.WithType(replacement.WithTriviaFrom(param.Type));
            newRoot = root.ReplaceNode(param, newParam);
        }
        else if (node is PredefinedTypeSyntax predefined)
        {
            newRoot = root.ReplaceNode(predefined, replacement.WithTriviaFrom(predefined));
        }
        else
        {
            return document;
        }

        newRoot = UsingDirectives.AddIfMissing(newRoot, "System.IO");
        return document.WithSyntaxRoot(newRoot);
    }
}
