using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128044: adds <c lang="csharp">: IDisposable</c> to the type declaration
///     and implements a <c lang="csharp">Dispose()</c> method stub.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncDisposableWithoutDisposableCodeFixProvider))]
[Shared]
public sealed class AsyncDisposableWithoutDisposableCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [AsyncDisposableWithoutDisposableAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var typeDecl = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (typeDecl is null)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Implement IDisposable",
                ct => AddIDisposableAsync(context.Document, typeDecl, ct),
                nameof(AsyncDisposableWithoutDisposableCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
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

        var newTypeDecl = typeDecl.WithBaseList(newBaseList.WithTrailingTrivia(SyntaxFactory.LineFeed));

        // A type that already declares Dispose() satisfies IDisposable through that
        // member. Inserting a second one is CS0111.
        if (!HasParameterlessDispose(typeDecl))
        {
            var disposeMethod = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    "Dispose")
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
                .WithBody(SyntaxFactory.Block())
                .WithAdditionalAnnotations(Formatter.Annotation);

            newTypeDecl = newTypeDecl.WithMembers(newTypeDecl.Members.Add(disposeMethod));
        }

        var newRoot = UsingDirectives.AddIfMissing(
            root.ReplaceNode(typeDecl, newTypeDecl), "System");
        return document.WithSyntaxRoot(newRoot);
    }

    private static bool HasParameterlessDispose(TypeDeclarationSyntax typeDecl)
    {
        foreach (var member in typeDecl.Members)
        {
            if (member is MethodDeclarationSyntax method
                && method.ParameterList.Parameters.Count == 0
                && string.Equals(method.Identifier.ValueText, "Dispose", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
