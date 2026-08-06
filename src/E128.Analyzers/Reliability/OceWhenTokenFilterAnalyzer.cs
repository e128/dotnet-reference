using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OceWhenTokenFilterAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128100";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Catch filter reads negated token state on OperationCanceledException",
        "Catch filter reads !token.IsCancellationRequested — the token-state read races with the throw; rethrow unconditionally instead",
        "Reliability",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCatchClause, SyntaxKind.CatchClause);
    }

    private static void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;

        if (catchClause.Filter is null
            || !IsOceOrTce(context, catchClause.Declaration?.Type)
            || !IsNegatedCancellationRequestedFilter(context, catchClause.Filter.FilterExpression))
        {
            return;
        }

        var span = TextSpan.FromBounds(
            catchClause.CatchKeyword.SpanStart,
            catchClause.Filter.CloseParenToken.Span.End);
        context.ReportDiagnostic(Diagnostic.Create(Rule, Location.Create(catchClause.SyntaxTree, span)));
    }

    private static bool IsOceOrTce(SyntaxNodeAnalysisContext context, TypeSyntax? type)
    {
        if (type is null)
        {
            return false;
        }

        var fullName = context.SemanticModel.GetTypeInfo(type, context.CancellationToken).Type?.ToDisplayString();
        return fullName is "System.OperationCanceledException" or "System.Threading.Tasks.TaskCanceledException";
    }

    private static bool IsNegatedCancellationRequestedFilter(SyntaxNodeAnalysisContext context, ExpressionSyntax filterExpression)
    {
        return Unwrap(filterExpression) is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } notExpression
               && Unwrap(notExpression.Operand) is MemberAccessExpressionSyntax
               {
                   Name.Identifier.ValueText: "IsCancellationRequested"
               } tokenAccess
               && IsCancellationTokenTyped(context, tokenAccess.Expression);
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static bool IsCancellationTokenTyped(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        var type = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type;
        return type is { Name: "CancellationToken" }
               && string.Equals(type.ContainingNamespace?.ToDisplayString(), "System.Threading", StringComparison.Ordinal);
    }
}
