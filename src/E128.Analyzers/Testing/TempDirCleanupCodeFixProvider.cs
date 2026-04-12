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
/// Code fix for E128054: adds <c>IAsyncLifetime</c> to the class and generates
/// <c>InitializeAsync</c> (returns <c>Task.CompletedTask</c>) and <c>DisposeAsync</c>
/// (calls <c>Directory.Delete</c> on the temp path).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TempDirCleanupCodeFixProvider))]
[Shared]
public sealed class TempDirCleanupCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TempDirCleanupAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

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
                title: "Implement IAsyncLifetime with temp cleanup",
                createChangedDocument: ct => AddAsyncLifetimeAsync(context.Document, classDecl, ct),
                equivalenceKey: nameof(TempDirCleanupCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddAsyncLifetimeAsync(
        Document document,
        ClassDeclarationSyntax classDecl,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var asyncLifetimeBase = SyntaxFactory.SimpleBaseType(
            SyntaxFactory.ParseTypeName("IAsyncLifetime"));

        var newBaseList = classDecl.BaseList is not null
            ? classDecl.BaseList.AddTypes(asyncLifetimeBase)
            : SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(asyncLifetimeBase));

        var initMethod = BuildInitializeMethod();
        var disposeMethod = BuildDisposeMethod();

        var newMembers = classDecl.Members.Add(initMethod).Add(disposeMethod);

        var newClassDecl = classDecl
            .WithBaseList(newBaseList.WithTrailingTrivia(SyntaxFactory.LineFeed))
            .WithMembers(newMembers);

        var newRoot = root.ReplaceNode(classDecl, newClassDecl);
        return document.WithSyntaxRoot(newRoot);
    }

    private static MethodDeclarationSyntax BuildInitializeMethod()
    {
        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("Task"),
                "InitializeAsync")
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Task"),
                    SyntaxFactory.IdentifierName("CompletedTask"))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(SyntaxFactory.LineFeed)
            .WithTrailingTrivia(SyntaxFactory.LineFeed);
    }

    private static MethodDeclarationSyntax BuildDisposeMethod()
    {
        var deleteCall = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Directory"),
                    SyntaxFactory.IdentifierName("Delete")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("_path")),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                        SyntaxKind.TrueLiteralExpression)),
                }))));

        var returnStatement = SyntaxFactory.ReturnStatement(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("Task"),
                SyntaxFactory.IdentifierName("CompletedTask")));

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("Task"),
                "DisposeAsync")
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
            .WithBody(SyntaxFactory.Block(deleteCall, returnStatement))
            .WithLeadingTrivia(SyntaxFactory.LineFeed)
            .WithTrailingTrivia(SyntaxFactory.LineFeed);
    }
}
