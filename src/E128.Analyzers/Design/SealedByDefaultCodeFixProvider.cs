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
public sealed class SealedByDefaultCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [SealedByDefaultAnalyzer.DiagnosticId];

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

        var classDecl = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add sealed modifier",
                ct => AddSealedModifierAsync(context.Document, classDecl, ct),
                nameof(SealedByDefaultCodeFixProvider)),
            diagnostic);
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

        // Insert sealed after access modifiers and 'new'/'unsafe', before 'partial'.
        var insertIndex = 0;
        for (var i = 0; i < classDecl.Modifiers.Count; i++)
        {
            if (classDecl.Modifiers[i].Kind() is SyntaxKind.PublicKeyword
                or SyntaxKind.PrivateKeyword
                or SyntaxKind.ProtectedKeyword
                or SyntaxKind.InternalKeyword
                or SyntaxKind.NewKeyword
                or SyntaxKind.UnsafeKeyword)
            {
                insertIndex = i + 1;
            }
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
                classDecl.Modifiers.Insert(insertIndex, sealedToken));
        }

        var newRoot = root.ReplaceNode(classDecl, newClassDecl);
        return document.WithSyntaxRoot(newRoot);
    }
}
