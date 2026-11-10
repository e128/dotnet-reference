using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConcurrentDictionaryDoubleLookupAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128208";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Concurrent dictionary lookup done twice",
        "'ContainsKey' followed by an indexer on '{0}' does a double lookup and races with a writer — use 'TryGetValue'",
        "E128-Performance",
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

        context.RegisterCompilationStartAction(static startContext =>
        {
            var concurrentDictionary = startContext.Compilation.GetTypeByMetadataName(
                "System.Collections.Concurrent.ConcurrentDictionary`2");
            if (concurrentDictionary is null)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, concurrentDictionary),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol concurrentDictionary)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !string.Equals(access.Name.Identifier.ValueText, "ContainsKey", StringComparison.Ordinal))
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        if (receiverType is null
            || !TypeSymbols.MatchesOriginalDefinition(receiverType, concurrentDictionary))
        {
            return;
        }

        if (invocation.ArgumentList.Arguments.Count is not 1)
        {
            return;
        }

        if (FindOwningIfStatement(invocation) is not { } ifStatement)
        {
            return;
        }

        var key = invocation.ArgumentList.Arguments[0].Expression;
        var receiverSymbol = context.SemanticModel.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol;
        if (receiverSymbol is null)
        {
            return;
        }

        if (!ContainsMatchingRead(
                ifStatement.Statement,
                receiverSymbol,
                key,
                context.SemanticModel,
                context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), access.Expression.ToString()));
    }

    private static IfStatementSyntax? FindOwningIfStatement(InvocationExpressionSyntax invocation)
    {
        var condition = (ExpressionSyntax)invocation;
        while (condition.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            condition = parenthesized;
        }

        return condition.Parent is IfStatementSyntax ifStatement
            && ReferenceEquals(ifStatement.Condition, condition)
            ? ifStatement
            : null;
    }

    private static bool ContainsMatchingRead(
        StatementSyntax body,
        ISymbol receiver,
        ExpressionSyntax key,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var elementAccess in body.DescendantNodes().OfType<ElementAccessExpressionSyntax>())
        {
            if (elementAccess.ArgumentList.Arguments.Count is not 1 || IsWriteTarget(elementAccess))
            {
                continue;
            }

            var elementReceiver = semanticModel.GetSymbolInfo(elementAccess.Expression, cancellationToken).Symbol;
            if (!SymbolEqualityComparer.Default.Equals(elementReceiver, receiver))
            {
                continue;
            }

            if (KeysMatch(
                    elementAccess.ArgumentList.Arguments[0].Expression,
                    key,
                    semanticModel,
                    cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWriteTarget(ElementAccessExpressionSyntax elementAccess)
    {
        var parent = elementAccess.Parent;
        while (parent is ParenthesizedExpressionSyntax parenthesized)
        {
            parent = parenthesized.Parent;
        }

        return parent switch
        {
            AssignmentExpressionSyntax assignment => ReferenceEquals(assignment.Left, elementAccess),
            PrefixUnaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression
            } => true,
            PostfixUnaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression
            } => true,
            _ => false
        };
    }

    private static bool KeysMatch(
        ExpressionSyntax candidate,
        ExpressionSyntax key,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var candidateSymbol = semanticModel.GetSymbolInfo(candidate, cancellationToken).Symbol;
        var keySymbol = semanticModel.GetSymbolInfo(key, cancellationToken).Symbol;
        if (candidateSymbol is not null || keySymbol is not null)
        {
            return SymbolEqualityComparer.Default.Equals(candidateSymbol, keySymbol);
        }

        var candidateValue = semanticModel.GetConstantValue(candidate, cancellationToken);
        var keyValue = semanticModel.GetConstantValue(key, cancellationToken);
        return candidateValue.HasValue
               && keyValue.HasValue
               && Equals(candidateValue.Value, keyValue.Value);
    }
}
