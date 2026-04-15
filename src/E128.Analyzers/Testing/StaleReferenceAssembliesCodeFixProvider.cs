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
/// Code fix for E128062: replaces outdated <c>ReferenceAssemblies.Net.Net80</c> / <c>Net90</c>
/// with the minimum version matching the project target framework.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StaleReferenceAssembliesCodeFixProvider))]
[Shared]
public sealed class StaleReferenceAssembliesCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [StaleReferenceAssembliesAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        // Get the target version from the diagnostic arguments
        var targetVersion = diagnostic.Properties.GetValueOrDefault(StaleReferenceAssembliesAnalyzer.MinimumVersionOptionKey, "100");

        var newName = $"Net{targetVersion}";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Replace with ReferenceAssemblies.Net.{newName}",
                createChangedDocument: ct => ReplaceVersionAsync(context.Document, root, memberAccess, newName, ct),
                equivalenceKey: StaleReferenceAssembliesAnalyzer.DiagnosticId),
            diagnostic);
    }

    private static Task<Document> ReplaceVersionAsync(
        Document document,
        SyntaxNode root,
        MemberAccessExpressionSyntax memberAccess,
        string newName,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var newIdentifier = SyntaxFactory.IdentifierName(newName)
            .WithTriviaFrom(memberAccess.Name);

        var newMemberAccess = memberAccess.WithName(newIdentifier);
        var newRoot = root.ReplaceNode(memberAccess, newMemberAccess);

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
