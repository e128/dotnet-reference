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

/// <summary>
///     Code fix for E128044: adds <c>: IDisposable</c> to the type declaration
///     and implements a <c>Dispose()</c> method stub.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncDisposableWithoutDisposableCodeFixProvider))]
[Shared]
public sealed class AsyncDisposableWithoutDisposableCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [AsyncDisposableWithoutDisposableAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
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

        var typeDecl = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (typeDecl is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Implement IDisposable",
                ct => AddIDisposableAsync(context.Document, typeDecl, ct),
                nameof(AsyncDisposableWithoutDisposableCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddIDisposableAsync(
        Document document,
        TypeDeclarationSyntax typeDecl,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Add IDisposable to the base list.
        var disposableBase = SyntaxFactory.SimpleBaseType(
            SyntaxFactory.ParseTypeName("IDisposable"));

        var newBaseList = typeDecl.BaseList is not null
            ? typeDecl.BaseList.AddTypes(disposableBase)
            : SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(disposableBase));

        // Create Dispose() method stub.
        var disposeMethod = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                "Dispose")
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
            .WithBody(SyntaxFactory.Block())
            .WithLeadingTrivia(SyntaxFactory.LineFeed)
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        var existingMembers = typeDecl.Members;
        var newMembers = existingMembers.Add(disposeMethod);

        var newTypeDecl = typeDecl
            .WithBaseList(newBaseList.WithTrailingTrivia(SyntaxFactory.LineFeed))
            .WithMembers(newMembers);

        var newRoot = root.ReplaceNode(typeDecl, newTypeDecl);
        return document.WithSyntaxRoot(newRoot);
    }
}
