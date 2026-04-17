using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncVoidCodeFixProvider))]
[Shared]
public sealed class AsyncVoidCodeFixProvider : CodeFixProvider
{
    private const string TaskTypeName = "Task";
    private const string TaskNamespace = "System.Threading.Tasks";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [AsyncVoidAnalyzer.DiagnosticId];

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

        var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Change return type to Task",
                ct => ApplyFixAsync(context.Document, method, ct),
                nameof(AsyncVoidCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        MethodDeclarationSyntax method,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var taskType = SyntaxFactory.IdentifierName(TaskTypeName)
            .WithTriviaFrom(method.ReturnType);

        var newMethod = method.WithReturnType(taskType);
        var newRoot = root.ReplaceNode(method, newMethod);

        newRoot = EnsureUsingDirective(newRoot, TaskNamespace);

        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode EnsureUsingDirective(SyntaxNode root, string namespaceName)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        var parts = namespaceName.Split('.');
        var alreadyHasUsing = compilationUnit.Usings.Any(u =>
            u.Name is not null && string.Equals(u.Name.ToString(), namespaceName, StringComparison.Ordinal));

        if (alreadyHasUsing)
        {
            return root;
        }

        NameSyntax name = SyntaxFactory.IdentifierName(parts[0]);
        for (var i = 1; i < parts.Length; i++)
        {
            name = SyntaxFactory.QualifiedName(name, SyntaxFactory.IdentifierName(parts[i]));
        }

        var usingDirective = SyntaxFactory.UsingDirective(name)
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        return compilationUnit.AddUsings(usingDirective);
    }
}
