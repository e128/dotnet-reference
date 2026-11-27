using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Security;

/// <summary>
///     Code fix for E128256: replaces a <c lang="csharp">.FullName</c> logger argument with
///     <c lang="csharp">.Name</c>, which keeps the file name and drops the local directory layout.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LogPathLeakCodeFixProvider))]
[Shared]
public sealed class LogPathLeakCodeFixProvider : E128CodeFixProviderBase
{
    private const string Title = "Log the file name instead";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [LogPathLeakAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var memberAccess = node.FirstAncestorOrSelf<MemberAccessExpressionSyntax>();

        if (memberAccess is null
            || !string.Equals(
                memberAccess.Name.Identifier.ValueText,
                LogPathLeakAnalyzer.FullNamePropertyName,
                StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var replacement = memberAccess.WithName(
            SyntaxFactory.IdentifierName(LogPathLeakAnalyzer.NamePropertyName)
                .WithTriviaFrom(memberAccess.Name));

        CodeFixRegistration.Register(
            context,
            diagnostic,
            Title,
            nameof(LogPathLeakCodeFixProvider),
            ct => ReplaceAsync(context.Document, memberAccess, replacement, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        MemberAccessExpressionSyntax original,
        MemberAccessExpressionSyntax replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        return root is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(original, replacement));
    }
}
