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
public sealed class AsyncVoidCodeFixProvider : E128CodeFixProviderBase
{
    private const string TaskTypeName = "Task";
    private const string TaskNamespace = "System.Threading.Tasks";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [AsyncVoidAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return Task.CompletedTask;
        }

        // An override or an explicit interface implementation inherits its void return type from the
        // base member or the interface. Rewriting only this declaration gives CS0508 or CS0738.
        if (method.Modifiers.Any(SyntaxKind.OverrideKeyword) || method.ExplicitInterfaceSpecifier is not null)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Change return type to Task",
                ct => ApplyFixAsync(context.Document, method, ct),
                nameof(AsyncVoidCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
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

        newRoot = UsingDirectives.AddIfMissing(newRoot, TaskNamespace);

        return document.WithSyntaxRoot(newRoot);
    }
}
