using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TextContentGuardAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128077";

    internal const int MinimumGuardThreshold = 100;

    private static readonly string[] MatchMethods =
        ["Contains", "StartsWith", "EndsWith", "Equals"];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "TextContent string match requires a preceding length guard",
        "TextContent.{0}() must be preceded by a TextContent.Length guard (> {1} chars) in the same && expression to prevent matching ancestor elements",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "TextContent aggregates all descendant text. Without a length guard before the string match, " +
        "ancestor elements can accidentally match, causing unintended DOM removal. " +
        "Use: e.TextContent.Length < N && e.TextContent.Contains(\"...\") where N > 100.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!TryGetTextContentMatchMethod(invocation, out var methodName))
        {
            return;
        }

        if (!IsAngleSharpTextContent(context, invocation))
        {
            return;
        }

        var receiver = GetReceiverExpression(invocation);
        if (receiver is null)
        {
            return;
        }

        if (!NeedsGuardDiagnostic(invocation, receiver))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName, MinimumGuardThreshold));
    }

    private static bool TryGetTextContentMatchMethod(
        InvocationExpressionSyntax invocation,
        out string methodName)
    {
        methodName = string.Empty;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var method = memberAccess.Name.Identifier.ValueText;
        if (!Array.Exists(MatchMethods, m => string.Equals(m, method, StringComparison.Ordinal)))
        {
            return false;
        }

        if (memberAccess.Expression is not MemberAccessExpressionSyntax receiverAccess)
        {
            return false;
        }

        if (!string.Equals(receiverAccess.Name.Identifier.ValueText, "TextContent", StringComparison.Ordinal))
        {
            return false;
        }

        methodName = method;
        return true;
    }

    private static bool IsAngleSharpTextContent(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var textContentAccess = (MemberAccessExpressionSyntax)memberAccess.Expression;

        var symbolInfo = context.SemanticModel.GetSymbolInfo(textContentAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IPropertySymbol property)
        {
            return false;
        }

        var ns = property.ContainingType?.ContainingNamespace?.ToDisplayString();
        return ns is not null && ns.StartsWith("AngleSharp", StringComparison.Ordinal);
    }

    private static ExpressionSyntax? GetReceiverExpression(InvocationExpressionSyntax invocation)
    {
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var textContentAccess = memberAccess.Expression as MemberAccessExpressionSyntax;
        return textContentAccess?.Expression;
    }

    private static bool NeedsGuardDiagnostic(
        InvocationExpressionSyntax matchInvocation,
        ExpressionSyntax receiverExpression)
    {
        SyntaxNode current = matchInvocation;
        while (current.Parent is not null)
        {
            current = current.Parent;

            if (current is LambdaExpressionSyntax)
            {
                return true;
            }

            if (current is not BinaryExpressionSyntax binary || !binary.IsKind(SyntaxKind.LogicalAndExpression))
            {
                continue;
            }

            var rootAnd = ClimbToRootBinary(binary, SyntaxKind.LogicalAndExpression);
            if (HasValidGuardInAndChain(rootAnd, matchInvocation, receiverExpression))
            {
                return false;
            }

            current = rootAnd;
        }

        return true;
    }

    private static BinaryExpressionSyntax ClimbToRootBinary(
        BinaryExpressionSyntax binary,
        SyntaxKind kind)
    {
        var current = binary;
        while (current.Parent is BinaryExpressionSyntax parent && parent.IsKind(kind))
        {
            current = parent;
        }

        return current;
    }

    private static bool HasValidGuardInAndChain(
        BinaryExpressionSyntax rootAnd,
        InvocationExpressionSyntax matchInvocation,
        ExpressionSyntax receiverExpression)
    {
        var operands = new List<ExpressionSyntax>();
        FlattenBinaryChain(rootAnd, SyntaxKind.LogicalAndExpression, operands);

        foreach (var operand in operands)
        {
            if (IsSmallLengthGuard(operand, receiverExpression))
            {
                return true;
            }
        }

        var matchIndex = FindOperandIndex(operands, matchInvocation);
        if (matchIndex <= 0)
        {
            return false;
        }

        for (var i = 0; i < matchIndex; i++)
        {
            if (IsLargeLengthGuard(operands[i], receiverExpression))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSmallLengthGuard(ExpressionSyntax expr, ExpressionSyntax receiverExpression)
    {
        return TryGetLengthGuardValue(expr, receiverExpression, out var n) && n <= MinimumGuardThreshold;
    }

    private static bool IsLargeLengthGuard(ExpressionSyntax expr, ExpressionSyntax receiverExpression)
    {
        return TryGetLengthGuardValue(expr, receiverExpression, out var n) && n > MinimumGuardThreshold;
    }

    private static bool TryGetLengthGuardValue(
        ExpressionSyntax expr,
        ExpressionSyntax receiverExpression,
        out int value)
    {
        value = 0;

        return expr is BinaryExpressionSyntax binary
               && (((binary.IsKind(SyntaxKind.LessThanExpression) || binary.IsKind(SyntaxKind.LessThanOrEqualExpression))
                    && IsTextContentLengthAccess(binary.Left, receiverExpression)
                    && TryGetIntLiteral(binary.Right, out value))
                   || ((binary.IsKind(SyntaxKind.GreaterThanExpression) || binary.IsKind(SyntaxKind.GreaterThanOrEqualExpression))
                       && IsTextContentLengthAccess(binary.Right, receiverExpression)
                       && TryGetIntLiteral(binary.Left, out value)));
    }

    private static bool IsTextContentLengthAccess(
        ExpressionSyntax expr,
        ExpressionSyntax receiverExpression)
    {
        return expr is MemberAccessExpressionSyntax lengthAccess
               && string.Equals(lengthAccess.Name.Identifier.ValueText, "Length", StringComparison.Ordinal)
               && lengthAccess.Expression is MemberAccessExpressionSyntax textContentAccess
               && string.Equals(textContentAccess.Name.Identifier.ValueText, "TextContent", StringComparison.Ordinal)
               && SyntaxFactory.AreEquivalent(textContentAccess.Expression, receiverExpression);
    }

    private static bool TryGetIntLiteral(ExpressionSyntax expr, out int value)
    {
        value = 0;
        return expr is LiteralExpressionSyntax literal
               && literal.IsKind(SyntaxKind.NumericLiteralExpression)
               && int.TryParse(literal.Token.ValueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static void FlattenBinaryChain(
        ExpressionSyntax expr,
        SyntaxKind kind,
        List<ExpressionSyntax> result)
    {
        if (expr is BinaryExpressionSyntax binary && binary.IsKind(kind))
        {
            FlattenBinaryChain(binary.Left, kind, result);
            FlattenBinaryChain(binary.Right, kind, result);
        }
        else
        {
            result.Add(expr);
        }
    }

    private static int FindOperandIndex(
        List<ExpressionSyntax> operands,
        SyntaxNode target)
    {
        for (var i = 0; i < operands.Count; i++)
        {
            if (ContainsDescendantOrSelf(operands[i], target))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ContainsDescendantOrSelf(SyntaxNode container, SyntaxNode target)
    {
        if (container == target)
        {
            return true;
        }

        foreach (var node in container.DescendantNodes())
        {
            if (node == target)
            {
                return true;
            }
        }

        return false;
    }
}
