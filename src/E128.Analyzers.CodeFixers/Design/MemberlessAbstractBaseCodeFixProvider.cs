using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MemberlessAbstractBaseCodeFixProvider))]
[Shared]
public sealed class MemberlessAbstractBaseCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [MemberlessAbstractBaseAnalyzer.DiagnosticId, AbstractBaseWithMembersAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not TypeDeclarationSyntax declaration)
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(
            context,
            diagnostic,
            "Declare closed instead of abstract",
            nameof(MemberlessAbstractBaseCodeFixProvider),
            ct => ReplaceAbstractWithClosedAsync(context.Document, root, declaration, ct));

        return Task.CompletedTask;
    }

    private static Task<Document> ReplaceAbstractWithClosedAsync(
        Document document,
        SyntaxNode root,
        TypeDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        var modifierIndex = declaration.Modifiers.IndexOf(SyntaxKind.AbstractKeyword);
        if (modifierIndex < 0)
        {
            return Task.FromResult(document);
        }

        var abstractToken = declaration.Modifiers[modifierIndex];

        var closedToken = SyntaxFactory.Token(SyntaxKind.ClosedKeyword)
            .WithTriviaFrom(abstractToken);
        var newRoot = root.ReplaceToken(abstractToken, closedToken);

        _ = cancellationToken;
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
