using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LoggerArgumentGuardAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128122";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Guard an expensive log argument with IsEnabled",
        "This log call builds its message before the level check. Wrap it in an IsEnabled guard or use [LoggerMessage].",
        "Performance",
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
        context.RegisterCompilationStartAction(start =>
        {
            var loggerExtensions = MetadataTypes.Find(
                start.Compilation,
                "Microsoft.Extensions.Logging.LoggerExtensions");
            if (loggerExtensions.IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, loggerExtensions),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> loggerExtensions)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !TryGetLevel(memberAccess.Name.Identifier.ValueText, out var level))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !MetadataTypes.Contains(loggerExtensions, method.ContainingType))
        {
            return;
        }

        if (!HasExpensiveArgument(invocation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (IsInsideEnabledGuard(invocation, memberAccess.Expression, level, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    // A guard counts when the call sits in the "then" branch of an `if` whose condition checks
    // the same logger and level, or when an earlier `if (!guard) return;` exits first.
    private static bool IsInsideEnabledGuard(
        InvocationExpressionSyntax invocation,
        ExpressionSyntax receiver,
        string level,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        SyntaxNode child = invocation;

        for (var parent = child.Parent; parent is not null; child = parent, parent = parent.Parent)
        {
            if (parent is IfStatementSyntax ifStatement
                && ifStatement.Statement == child
                && ConditionHasGuard(ifStatement.Condition, receiver, level, model, cancellationToken))
            {
                return true;
            }

            if (parent is BlockSyntax block && HasEarlyReturnGuard(block, child, receiver, level))
            {
                return true;
            }

            if (parent is MemberDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool ConditionHasGuard(
        ExpressionSyntax condition,
        ExpressionSyntax receiver,
        string level,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        return ContainsGuard(condition, receiver, level)
            || condition.DescendantNodesAndSelf()
                .Where(node => node is IdentifierNameSyntax or InvocationExpressionSyntax
                    && node.Parent is not PrefixUnaryExpressionSyntax)
                .SelectMany(node => GetBackingExpressions(model.GetSymbolInfo(node, cancellationToken).Symbol))
                .Any(expression => ContainsGuard(expression, receiver, level));
    }

    private static bool ContainsGuard(SyntaxNode node, ExpressionSyntax receiver, string level)
    {
        return node
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(candidate => IsGuardCall(candidate, receiver, level)
                && !IsWeakened(candidate, node));
    }

    // A guard only proves the level is enabled when it is not negated and not one side of
    // `||`. `if (verbose || logger.IsEnabled(Information))` still logs when logging is off.
    private static bool IsWeakened(SyntaxNode guard, SyntaxNode condition)
    {
        for (var parent = guard.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is PrefixUnaryExpressionSyntax negation
                && negation.IsKind(SyntaxKind.LogicalNotExpression))
            {
                return true;
            }

            if (parent is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.LogicalOrExpression))
            {
                return true;
            }

            if (parent == condition)
            {
                return false;
            }
        }

        return false;
    }

    // One level of indirection: a local that holds the check, or a helper method or property that returns it.
    private static IEnumerable<SyntaxNode> GetBackingExpressions(ISymbol? symbol)
    {
        var references = symbol is ILocalSymbol or IMethodSymbol or IPropertySymbol
            ? symbol.DeclaringSyntaxReferences
            : [];

        foreach (var reference in references)
        {
            var body = reference.GetSyntax() switch
            {
                VariableDeclaratorSyntax declarator => declarator.Initializer?.Value,
                MethodDeclarationSyntax method => method.ExpressionBody?.Expression ?? (SyntaxNode?)method.Body,
                PropertyDeclarationSyntax property => property.ExpressionBody?.Expression
                    ?? (SyntaxNode?)property.AccessorList?.Accessors.FirstOrDefault()?.Body
                    ?? property.AccessorList?.Accessors.FirstOrDefault()?.ExpressionBody?.Expression,
                _ => null,
            };

            if (body is not null)
            {
                yield return body;
            }
        }
    }

    private static bool HasEarlyReturnGuard(BlockSyntax block, SyntaxNode child, ExpressionSyntax receiver, string level)
    {
        var index = block.Statements.IndexOf((StatementSyntax)child);
        for (var i = 0; i < index; i++)
        {
            if (block.Statements[i] is IfStatementSyntax
                {
                    Condition: PrefixUnaryExpressionSyntax { Operand: var operand } negation,
                } earlier
                && negation.IsKind(SyntaxKind.LogicalNotExpression)
                && Unparenthesize(operand) is InvocationExpressionSyntax guard
                && IsGuardCall(guard, receiver, level)
                && (earlier.Statement is ReturnStatementSyntax
                    || earlier.Statement is BlockSyntax { Statements: { Count: 1 } returnStatements }
                        && returnStatements[0] is ReturnStatementSyntax))
            {
                return true;
            }
        }

        return false;
    }

    private static ExpressionSyntax Unparenthesize(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static bool IsGuardCall(InvocationExpressionSyntax candidate, ExpressionSyntax receiver, string level)
    {
        if (candidate.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "IsEnabled" } access
            || !SyntaxFactory.AreEquivalent(access.Expression, receiver)
            || candidate.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        var levelName = candidate.ArgumentList.Arguments[0].Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

        return string.Equals(levelName, level, StringComparison.Ordinal);
    }

    internal static bool HasExpensiveArgument(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is InterpolatedStringExpressionSyntax interpolated
                && !semanticModel.GetConstantValue(interpolated, cancellationToken).HasValue)
            {
                return true;
            }

            if (argument.Expression is BinaryExpressionSyntax binary
                && binary.IsKind(SyntaxKind.AddExpression)
                && !semanticModel.GetConstantValue(binary, cancellationToken).HasValue
                && semanticModel.GetTypeInfo(binary, cancellationToken).Type?.SpecialType == SpecialType.System_String)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryGetLevel(string methodName, out string level)
    {
        level = methodName switch
        {
            "LogTrace" => "Trace",
            "LogDebug" => "Debug",
            "LogInformation" => "Information",
            "LogWarning" => "Warning",
            "LogError" => "Error",
            "LogCritical" => "Critical",
            _ => string.Empty,
        };

        return level.Length > 0;
    }
}
