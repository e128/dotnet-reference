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
///     Code fix for E128060: appends <c lang="csharp">.AsReadOnly()</c> to the return expression
///     so the caller receives a <c lang="csharp">ReadOnlyDictionary&lt;K,V&gt;</c> wrapper rather than
///     the raw mutable dictionary.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DictionaryAsReadOnlyCodeFixProvider))]
[Shared]
public sealed class DictionaryAsReadOnlyCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [DictionaryAsReadOnlyAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        // The diagnostic is reported on the expression being returned (e.g. the identifier
        // '_dict' or the 'new Dictionary<>()' creation), not the enclosing return statement.
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is not ExpressionSyntax expression)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Return .AsReadOnly()",
                ct => AppendAsReadOnlyAsync(context.Document, expression, ct),
                nameof(DictionaryAsReadOnlyCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
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
