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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SealedByDefaultCodeFixProvider))]
[Shared]
public sealed class SealedByDefaultCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [SealedByDefaultAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var classDecl = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl is null)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add sealed modifier",
                ct => AddSealedModifierAsync(context.Document, classDecl, ct),
                nameof(SealedByDefaultCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Document> AddSealedModifierAsync(
        Document document,
        ClassDeclarationSyntax classDecl,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var sealedToken = SyntaxFactory.Token(SyntaxKind.SealedKeyword)
            .WithTrailingTrivia(SyntaxFactory.Space);

        ClassDeclarationSyntax newClassDecl;

        if (!classDecl.Modifiers.Any())
        {
            // No existing modifiers — move leading trivia from 'class' keyword to 'sealed'.
            sealedToken = sealedToken.WithLeadingTrivia(classDecl.Keyword.LeadingTrivia);
            newClassDecl = classDecl
                .WithModifiers(SyntaxFactory.TokenList(sealedToken))
                .WithKeyword(classDecl.Keyword.WithLeadingTrivia(SyntaxTriviaList.Empty));
        }
        else
        {
            newClassDecl = classDecl.WithModifiers(
                InModifierHelper.AddModifier(classDecl.Modifiers, SyntaxKind.SealedKeyword));
        }

        var newRoot = root.ReplaceNode(classDecl, newClassDecl);
        return document.WithSyntaxRoot(newRoot);
    }
}
