using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128039: Flags filtered catch blocks (<c>catch (Exception ex) when (ex is not ...)</c>)
///     that do not exclude <see cref="OperationCanceledException" /> (or its subclass
///     <see cref="System.Threading.Tasks.TaskCanceledException" />). Swallowing cancellation
///     silently breaks cooperative cancellation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CatchFilterOceAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128039";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Catch filter must exclude OperationCanceledException",
        "Catch filter does not exclude OperationCanceledException — swallowing cancellation breaks cooperative cancellation",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Filtered catch blocks that exclude specific exception types (e.g., OutOfMemoryException) " +
        "must also exclude OperationCanceledException (or TaskCanceledException). Without this, " +
        "cancellation tokens are silently swallowed, breaking cooperative cancellation patterns.");

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

        if (catchClause.Filter is null)
        {
            return;
        }

        var filterExpression = catchClause.Filter.FilterExpression;

        if (!IsExceptionTypeExclusionPattern(filterExpression))
        {
            return;
        }

        if (ExcludesOperationCanceledException(filterExpression, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (PrecedingCatchHandlesOce(catchClause, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        var span = TextSpan.FromBounds(
            catchClause.CatchKeyword.SpanStart,
            catchClause.Filter.CloseParenToken.Span.End);
        var location = Location.Create(catchClause.SyntaxTree, span);
        context.ReportDiagnostic(Diagnostic.Create(Rule, location));
    }

    private static bool PrecedingCatchHandlesOce(
        CatchClauseSyntax catchClause,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (catchClause.Parent is not TryStatementSyntax tryStatement)
        {
            return false;
        }

        foreach (var precedingCatch in tryStatement.Catches)
        {
            if (precedingCatch == catchClause)
            {
                break;
            }

            if (precedingCatch.Declaration?.Type is null)
            {
                continue;
            }

            var typeInfo = semanticModel.GetTypeInfo(precedingCatch.Declaration.Type, cancellationToken);
            if (typeInfo.Type is null)
            {
                continue;
            }

            var fullName = typeInfo.Type.ToDisplayString();
            if (fullName is "System.OperationCanceledException" or "System.Threading.Tasks.TaskCanceledException")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExceptionTypeExclusionPattern(ExpressionSyntax expression)
    {
        return expression switch
        {
            IsPatternExpressionSyntax { Pattern: UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } } => true,
            IsPatternExpressionSyntax { Pattern: BinaryPatternSyntax pattern } => ContainsNotPattern(pattern),
            _ => false
        };
    }

    private static bool ContainsNotPattern(PatternSyntax pattern)
    {
        return pattern switch
        {
            UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } => true,
            BinaryPatternSyntax binary => ContainsNotPattern(binary.Left) || ContainsNotPattern(binary.Right),
            _ => false
        };
    }

    private static bool ExcludesOperationCanceledException(
        ExpressionSyntax filterExpression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var typeNode in filterExpression.DescendantNodes())
        {
            if (typeNode is not TypeSyntax typeSyntax)
            {
                continue;
            }

            var typeInfo = semanticModel.GetTypeInfo(typeSyntax, cancellationToken);
            if (typeInfo.Type is null)
            {
                continue;
            }

            var fullName = typeInfo.Type.ToDisplayString();
            if (fullName is "System.OperationCanceledException" or "System.Threading.Tasks.TaskCanceledException")
            {
                return true;
            }
        }

        return false;
    }
}
