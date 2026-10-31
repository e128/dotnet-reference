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
///     Code fix for E128054: adds <c lang="csharp">IAsyncLifetime</c> to the class and generates
///     <c lang="csharp">InitializeAsync</c> (returns <c lang="csharp">ValueTask.CompletedTask</c>) and
///     <c lang="csharp">DisposeAsync</c> (calls <c lang="csharp">Directory.Delete</c> on the temp path).
///     xUnit v3 declares both members with <c lang="csharp">ValueTask</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TempDirCleanupCodeFixProvider))]
[Shared]
public sealed class TempDirCleanupCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TempDirCleanupAnalyzer.DiagnosticId];

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

        // The cleanup call needs the name of the member that holds the temp path.
        if (!diagnostic.Properties.TryGetValue(TempDirCleanupAnalyzer.PropMemberName, out var memberName)
            || memberName is not { Length: > 0 })
        {
            return;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var classDecl = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Implement IAsyncLifetime with temp cleanup",
                ct => AddAsyncLifetimeAsync(context.Document, classDecl, memberName, ct),
                nameof(TempDirCleanupCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddAsyncLifetimeAsync(
        Document document,
        ClassDeclarationSyntax classDecl,
        string memberName,
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
        var disposeMethod = BuildDisposeMethod(memberName);

        var newMembers = classDecl.Members.Add(initMethod).Add(disposeMethod);

        var newClassDecl = classDecl
            .WithBaseList(newBaseList.WithTrailingTrivia(SyntaxFactory.LineFeed))
            .WithMembers(newMembers);

        var newRoot = root.ReplaceNode(classDecl, newClassDecl);

        // Both types the generated body names need a using. Do not add a using for the interface.
        newRoot = UsingDirectives.AddIfMissing(newRoot, "System.IO");
        newRoot = UsingDirectives.AddIfMissing(newRoot, "System.Threading.Tasks");

        return document.WithSyntaxRoot(newRoot);
    }

    private static MethodDeclarationSyntax BuildInitializeMethod()
    {
        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("ValueTask"),
                "InitializeAsync")
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("ValueTask"),
                    SyntaxFactory.IdentifierName("CompletedTask"))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(SyntaxFactory.LineFeed)
            .WithTrailingTrivia(SyntaxFactory.LineFeed);
    }

    private static MethodDeclarationSyntax BuildDisposeMethod(string memberName)
    {
        var deleteCall = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Directory"),
                    SyntaxFactory.IdentifierName("Delete")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
                    [
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName(memberName)),
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            SyntaxKind.TrueLiteralExpression))
                    ]))));

        var returnStatement = SyntaxFactory.ReturnStatement(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("ValueTask"),
                SyntaxFactory.IdentifierName("CompletedTask")));

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("ValueTask"),
                "DisposeAsync")
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
            .WithBody(SyntaxFactory.Block(deleteCall, returnStatement))
            .WithLeadingTrivia(SyntaxFactory.LineFeed)
            .WithTrailingTrivia(SyntaxFactory.LineFeed);
    }
}
