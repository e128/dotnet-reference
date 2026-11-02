using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128077: prepends a <c lang="csharp">TextContent.Length</c> guard to the
///     enclosing <c lang="csharp">&amp;&amp;</c> expression so the match cannot reach an ancestor
///     element.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TextContentGuardCodeFixProvider))]
[Shared]
public sealed class TextContentGuardCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TextContentGuardAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax matchAccess
            || matchAccess.Expression is not MemberAccessExpressionSyntax textContentAccess)
        {
            return Task.CompletedTask;
        }

        // The guard has to sit to the left of the match in the same && expression, so the fix
        // extends the outermost enclosing && chain.
        ExpressionSyntax rootOperand = invocation;
        while (rootOperand.Parent is BinaryExpressionSyntax parent
               && parent.IsKind(SyntaxKind.LogicalAndExpression))
        {
            rootOperand = parent;
        }

        var receiverText = textContentAccess.Expression.ToString();
        var minimumLength = (TextContentGuardAnalyzer.MinimumGuardThreshold + 1)
            .ToString(CultureInfo.InvariantCulture);
        var guardText = $"{receiverText}.TextContent.Length < {minimumLength}";

        // A negated match takes the guard inside the negation. Without the parentheses the
        // rewrite would negate the guard alone.
        var needsParentheses = rootOperand.Parent is PrefixUnaryExpressionSyntax;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add a TextContent.Length guard",
                ct => AddGuardAsync(context.Document, rootOperand, guardText, needsParentheses, ct),
                nameof(TextContentGuardCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Document> AddGuardAsync(
        Document document,
        ExpressionSyntax rootOperand,
        string guardText,
        bool needsParentheses,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var operandText = rootOperand.WithoutLeadingTrivia().ToString();
        var combined = SyntaxFactory.ParseExpression($"{guardText} && {operandText}")
            .WithLeadingTrivia(rootOperand.GetLeadingTrivia());

        var replacement = needsParentheses
            ? SyntaxFactory.ParenthesizedExpression((BinaryExpressionSyntax)combined)
                .WithLeadingTrivia(rootOperand.GetLeadingTrivia())
            : combined;

        return document.WithSyntaxRoot(root.ReplaceNode(rootOperand, replacement));
    }
}
