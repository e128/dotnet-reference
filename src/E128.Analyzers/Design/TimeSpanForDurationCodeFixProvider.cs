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
///     Code fix for E128050: replaces the numeric type (int, long, float, double) with <c>TimeSpan</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TimeSpanForDurationCodeFixProvider))]
[Shared]
public sealed class TimeSpanForDurationCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TimeSpanForDurationAnalyzer.DiagnosticId];

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
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        context.RegisterCodeFix(
            CodeAction.Create(
                "Change type to TimeSpan",
                ct => ApplyFixAsync(context.Document, root, node, ct),
                nameof(TimeSpanForDurationCodeFixProvider)),
            diagnostic);
    }

    private static Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode root,
        SyntaxNode node,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var timeSpanType = SyntaxFactory.IdentifierName("TimeSpan");

        SyntaxNode newRoot;
        if (node is PropertyDeclarationSyntax property)
        {
            var newProperty = property.WithType(timeSpanType.WithTriviaFrom(property.Type));
            newRoot = root.ReplaceNode(property, newProperty);
        }
        else if (node is ParameterSyntax param && param.Type is not null)
        {
            var newParam = param.WithType(timeSpanType.WithTriviaFrom(param.Type));
            newRoot = root.ReplaceNode(param, newParam);
        }
        else
        {
            return Task.FromResult(document);
        }

        newRoot = EnsureUsingSystem(newRoot);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static SyntaxNode EnsureUsingSystem(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        if (compilationUnit.Usings.Any(u =>
                string.Equals(u.Name?.ToString(), "System", StringComparison.Ordinal)))
        {
            return root;
        }

        var usingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.IdentifierName("System"))
            .NormalizeWhitespace()
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        return compilationUnit.AddUsings(usingDirective);
    }
}
