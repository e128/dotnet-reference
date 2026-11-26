using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Testing;

/// <summary>
///     Code fix for E128062: replaces outdated <c lang="csharp">ReferenceAssemblies.Net.Net80</c> / <c lang="csharp">Net90</c>
///     with the minimum version matching the project target framework.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StaleReferenceAssembliesCodeFixProvider))]
[Shared]
public sealed class StaleReferenceAssembliesCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [StaleReferenceAssembliesAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var targetVersion = context.Diagnostics[0]
            .Properties.GetValueOrDefault(StaleReferenceAssembliesAnalyzer.MinimumVersionOptionKey, "100");

        var newName = $"Net{targetVersion}";

        RegisterNodeFix<MemberAccessExpressionSyntax>(
            context,
            root,
            $"Replace with ReferenceAssemblies.Net.{newName}",
            StaleReferenceAssembliesAnalyzer.DiagnosticId,
            (memberAccess, ct) => ReplaceVersionAsync(context.Document, root, memberAccess, newName, ct));

        return Task.CompletedTask;
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
