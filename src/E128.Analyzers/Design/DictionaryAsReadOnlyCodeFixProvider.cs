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
/// Code fix for E128060: appends <c>.AsReadOnly()</c> to the return expression
/// so the caller receives a <c>ReadOnlyDictionary&lt;K,V&gt;</c> wrapper rather than
/// the raw mutable dictionary.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DictionaryAsReadOnlyCodeFixProvider))]
[Shared]
public sealed class DictionaryAsReadOnlyCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DictionaryAsReadOnlyAnalyzer.DiagnosticId];

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
        // The diagnostic is reported on the expression being returned (e.g. the identifier
        // '_dict' or the 'new Dictionary<>()' creation), not the enclosing return statement.
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is not ExpressionSyntax expression)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Return .AsReadOnly()",
                createChangedDocument: ct => AppendAsReadOnlyAsync(context.Document, expression, ct),
                equivalenceKey: nameof(DictionaryAsReadOnlyCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AppendAsReadOnlyAsync(
        Document document,
        ExpressionSyntax expression,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var asReadOnly = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression.WithoutTrivia(),
                SyntaxFactory.IdentifierName("AsReadOnly")))
            .WithTriviaFrom(expression);

        var newRoot = root.ReplaceNode(expression, asReadOnly);
        return document.WithSyntaxRoot(newRoot);
    }
}
