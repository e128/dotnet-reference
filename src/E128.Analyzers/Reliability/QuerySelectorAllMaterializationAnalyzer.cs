using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QuerySelectorAllMaterializationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128076";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Materialize QuerySelectorAll result before iterating",
        "QuerySelectorAll returns a live DOM collection — add .ToList() before iterating to prevent mutation errors",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "AngleSharp's QuerySelectorAll returns a live HTMLCollection that reflects DOM mutations. " +
        "Iterating it directly inside a loop that modifies the DOM causes skipped or duplicate elements. " +
        "Call .ToList() to snapshot the collection before iterating.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ForEachStatement);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var forEach = (ForEachStatementSyntax)context.Node;
        var expression = forEach.Expression;

        foreach (var invocation in expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (TryReportUnmaterializedQsa(context, invocation, expression))
            {
                return;
            }
        }

        if (expression is IdentifierNameSyntax identifier)
        {
            AnalyzeVariableAssignment(context, identifier);
        }
    }

    private static bool TryReportUnmaterializedQsa(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax root)
    {
        if (!IsQuerySelectorAllInvocation(invocation))
        {
            return false;
        }

        if (HasMaterializingParent(invocation, root))
        {
            return false;
        }

        if (!IsAngleSharpMethod(context, invocation))
        {
            return false;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        return true;
    }

    private static void AnalyzeVariableAssignment(
        SyntaxNodeAnalysisContext context,
        IdentifierNameSyntax identifier)
    {
        var symbolInfo = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken);
        if (symbolInfo.Symbol is not ILocalSymbol local)
        {
            return;
        }

        foreach (var syntaxRef in local.DeclaringSyntaxReferences)
        {
            var declaratorNode = syntaxRef.GetSyntax(context.CancellationToken);
            if (declaratorNode is not VariableDeclaratorSyntax declarator || declarator.Initializer is null)
            {
                continue;
            }

            var initExpression = declarator.Initializer.Value;

            foreach (var invocation in initExpression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
            {
                if (!IsQuerySelectorAllInvocation(invocation))
                {
                    continue;
                }

                if (IsInsideLambda(invocation, initExpression))
                {
                    continue;
                }

                if (HasMaterializingParent(invocation, initExpression))
                {
                    continue;
                }

                if (!IsAngleSharpMethod(context, invocation))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
                return;
            }
        }
    }

    private static bool IsInsideLambda(SyntaxNode node, SyntaxNode boundary)
    {
        var current = node.Parent;
        while (current is not null && current != boundary)
        {
            if (current is LambdaExpressionSyntax)
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static bool IsAngleSharpMethod(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return false;
        }

        var ns = method.ContainingType?.ContainingNamespace?.ToDisplayString();
        return ns is not null && ns.StartsWith("AngleSharp", StringComparison.Ordinal);
    }

    private static bool IsQuerySelectorAllInvocation(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
            ? string.Equals(memberAccess.Name.Identifier.ValueText, "QuerySelectorAll", StringComparison.Ordinal)
            : invocation.Expression is IdentifierNameSyntax id
              && string.Equals(id.Identifier.ValueText, "QuerySelectorAll", StringComparison.Ordinal);
    }

    private static bool HasMaterializingParent(InvocationExpressionSyntax qsaNode, ExpressionSyntax root)
    {
        SyntaxNode current = qsaNode;

        while (current != root)
        {
            var parent = current.Parent;
            if (parent is null)
            {
                break;
            }

            if (parent is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Expression == current
                && memberAccess.Parent is InvocationExpressionSyntax parentInvocation)
            {
                var methodName = memberAccess.Name.Identifier.ValueText;
                if (string.Equals(methodName, "ToList", StringComparison.Ordinal)
                    || string.Equals(methodName, "ToArray", StringComparison.Ordinal))
                {
                    return true;
                }

                current = parentInvocation;
                continue;
            }

            break;
        }

        return false;
    }
}
