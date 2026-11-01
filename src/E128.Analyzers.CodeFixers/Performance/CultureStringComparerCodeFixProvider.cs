using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

/// <summary>
///     Code fix for E128132: replaces a culture comparer with
///     <c lang="csharp">StringComparer.Ordinal</c> or
///     <c lang="csharp">StringComparer.OrdinalIgnoreCase</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CultureStringComparerCodeFixProvider))]
[Shared]
public sealed class CultureStringComparerCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [CultureStringComparerAnalyzer.DiagnosticId];

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

        if (!diagnostic.Properties.TryGetValue(CultureStringComparerAnalyzer.ReplacementKey, out var replacement)
            || replacement is null)
        {
            return;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (node is not ExpressionSyntax expression)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Use StringComparer.{replacement}",
                ct => ReplaceComparerAsync(context.Document, expression, replacement, ct),
                CultureStringComparerAnalyzer.DiagnosticId),
            diagnostic);
    }

    private static async Task<Document> ReplaceComparerAsync(
        Document document,
        ExpressionSyntax expression,
        string replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        ExpressionSyntax replacementExpression;

        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            replacementExpression = memberAccess.WithName(
                SyntaxFactory.IdentifierName(
                    SyntaxFactory.Identifier(replacement).WithTriviaFrom(memberAccess.Name.Identifier)));
        }
        else if (expression is IdentifierNameSyntax identifier)
        {
            replacementExpression = SyntaxFactory.IdentifierName(
                SyntaxFactory.Identifier(replacement).WithTriviaFrom(identifier.Identifier));
        }
        else
        {
            return document;
        }

        return document.WithSyntaxRoot(root.ReplaceNode(expression, replacementExpression));
    }
}
