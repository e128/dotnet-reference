using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ByteSizeUnwrapViaCastCodeFixProvider))]
[Shared]
public sealed class ByteSizeUnwrapViaCastCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ByteSizeUnwrapViaCastAnalyzer.DiagnosticId];

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
        if (root.FindNode(diagnostic.Location.SourceSpan) is not CastExpressionSyntax castExpression)
        {
            return;
        }

        var inner = UnwrapParentheses(castExpression.Expression);
        if (inner is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        if (IsNarrowingCast(semanticModel, castExpression, memberAccess, context.CancellationToken))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove ByteSize cast",
                ct => RemoveCastAsync(context.Document, castExpression, memberAccess, ct),
                nameof(ByteSizeUnwrapViaCastCodeFixProvider)),
            diagnostic);
    }

    private static bool IsNarrowingCast(
        SemanticModel semanticModel,
        CastExpressionSyntax castExpression,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        var castTargetType = semanticModel.GetTypeInfo(castExpression.Type, cancellationToken).Type;

        return castTargetType is null
               || semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol is not IPropertySymbol propertySymbol
               || castTargetType.SpecialType != propertySymbol.Type.SpecialType;
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parens)
        {
            expression = parens.Expression;
        }

        return expression;
    }

    private static async Task<Document> RemoveCastAsync(
        Document document,
        CastExpressionSyntax castExpression,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var replacement = memberAccess
            .WithLeadingTrivia(castExpression.GetLeadingTrivia())
            .WithTrailingTrivia(castExpression.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(castExpression, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}
