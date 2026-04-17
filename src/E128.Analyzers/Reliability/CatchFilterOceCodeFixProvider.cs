using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128039: adds <c>OperationCanceledException</c> to the catch filter's
///     exclusion list. For <c>catch (Exception ex) when (ex is not FooException)</c>, produces
///     <c>catch (Exception ex) when (ex is not FooException and not OperationCanceledException)</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CatchFilterOceCodeFixProvider))]
[Shared]
public sealed class CatchFilterOceCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [CatchFilterOceAnalyzer.DiagnosticId];

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

        var catchClause = node.FirstAncestorOrSelf<CatchClauseSyntax>();
        if (catchClause?.Filter is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add OperationCanceledException to catch filter",
                ct => AddOceToFilterAsync(context.Document, root, catchClause, ct),
                nameof(CatchFilterOceCodeFixProvider)),
            diagnostic);
    }

    private static Task<Document> AddOceToFilterAsync(
        Document document,
        SyntaxNode root,
        CatchClauseSyntax catchClause,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var filter = catchClause.Filter!;
        var filterExpression = filter.FilterExpression;

        // The filter is `ex is not FooException` or `ex is not A and not B`.
        // We need to find the is-pattern and extend the pattern with `and not OperationCanceledException`.
        if (filterExpression is not IsPatternExpressionSyntax isPattern)
        {
            return Task.FromResult(document);
        }

        var ocePattern = SyntaxFactory.UnaryPattern(
            SyntaxFactory.Token(SyntaxKind.NotKeyword).WithLeadingTrivia(SyntaxFactory.Space),
            SyntaxFactory.ConstantPattern(
                SyntaxFactory.IdentifierName("OperationCanceledException").WithLeadingTrivia(SyntaxFactory.Space)));

        var newPattern = SyntaxFactory.BinaryPattern(
            SyntaxKind.AndPattern,
            isPattern.Pattern,
            SyntaxFactory.Token(SyntaxKind.AndKeyword)
                .WithLeadingTrivia(SyntaxFactory.Space)
                .WithTrailingTrivia(SyntaxFactory.Space),
            ocePattern);

        var newIsPattern = isPattern.WithPattern(newPattern);
        var newFilter = filter.WithFilterExpression(newIsPattern);
        var newCatchClause = catchClause.WithFilter(newFilter);

        var newRoot = root.ReplaceNode(catchClause, newCatchClause);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
