using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MultiStringEqualsOrChainAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128029";
    internal const int MinChainLength = 3;

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Replace multi-string OR-chain with HashSet.Contains",
        messageFormat: "Replace {0} '||'-chained string equality tests on '{1}' with a HashSet<string>.Contains() check",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.LogicalOrExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var orExpr = (BinaryExpressionSyntax)context.Node;

        if (orExpr.Parent is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression })
        {
            return;
        }

        var operands = FlattenOrChain(orExpr);
        if (operands.Count < MinChainLength)
        {
            return;
        }

        var infos = CollectStringEqualityInfos(context, operands);
        if (infos.Count < MinChainLength)
        {
            return;
        }

        var match = FindHomogeneousGroup(infos);
        if (match is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, orExpr.GetLocation(), match.Value.Count, match.Value.OperandText));
        }
    }

    private static List<StringEqualityInfo> CollectStringEqualityInfos(
        SyntaxNodeAnalysisContext context, List<ExpressionSyntax> operands)
    {
        var infos = new List<StringEqualityInfo>(operands.Count);
        foreach (var operand in operands)
        {
            var info = TryExtractStringEquality(context.SemanticModel, operand, context.CancellationToken);
            if (info is not null)
            {
                infos.Add(info.Value);
            }
        }

        return infos;
    }

    private static (int Count, string OperandText)? FindHomogeneousGroup(List<StringEqualityInfo> infos)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pivot in infos)
        {
            var pivotKey = pivot.OperandText + "\0" + pivot.ComparisonKey;
            if (!seen.Add(pivotKey))
            {
                continue;
            }

            var count = 0;
            foreach (var info in infos)
            {
                if (string.Equals(info.OperandText, pivot.OperandText, StringComparison.Ordinal)
                    && string.Equals(info.ComparisonKey, pivot.ComparisonKey, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            if (count >= MinChainLength)
            {
                return (count, pivot.OperandText);
            }
        }

        return null;
    }

    internal static List<ExpressionSyntax> FlattenOrChain(BinaryExpressionSyntax root)
    {
        var result = new List<ExpressionSyntax>();
        Flatten(root);
        return result;

        void Flatten(ExpressionSyntax expr)
        {
            if (expr is BinaryExpressionSyntax bin && bin.IsKind(SyntaxKind.LogicalOrExpression))
            {
                Flatten(bin.Left);
                Flatten(bin.Right);
            }
            else
            {
                result.Add(expr);
            }
        }
    }

    // Extracts string equality info from one leaf expression, or returns null if it doesn't qualify.
    // Handles two forms:
    //   Form 1: string.Equals(operand, "literal", StringComparison.X)
    //   Form 2: operand == "literal"  (identifier on left, string literal on right)
    internal static StringEqualityInfo? TryExtractStringEquality(
        SemanticModel semanticModel, ExpressionSyntax expr, CancellationToken cancellationToken)
    {
        // Form 1: string.Equals(operand, "literal", StringComparison.X)
        if (expr is InvocationExpressionSyntax invocation
            && invocation.Expression is MemberAccessExpressionSyntax
            {
                Expression: PredefinedTypeSyntax { Keyword.ValueText: "string" },
                Name.Identifier.ValueText: "Equals",
            }
            && invocation.ArgumentList.Arguments.Count == 3)
        {
            var args = invocation.ArgumentList.Arguments;
            var literalArg = args[1].Expression;

            if (!literalArg.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return null;
            }

            // Semantic verification: confirm the call resolves to System.String.Equals.
            var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
            return symbolInfo.Symbol is not IMethodSymbol method
                || method.ContainingType?.SpecialType != SpecialType.System_String
                ? null
                : new StringEqualityInfo(
                args[0].Expression.ToString(),
                ((LiteralExpressionSyntax)literalArg).Token.ValueText,
                args[2].Expression.ToString());
        }

        // Form 2: operand == "literal"
        return expr is BinaryExpressionSyntax
        {
            RawKind: (int)SyntaxKind.EqualsExpression,
            Left: var left,
            Right: LiteralExpressionSyntax rightLit,
        }
            && rightLit.IsKind(SyntaxKind.StringLiteralExpression)
            ? new StringEqualityInfo(left.ToString(), rightLit.Token.ValueText, "==")
            : null;
    }

    internal readonly struct StringEqualityInfo : IEquatable<StringEqualityInfo>
    {
        internal StringEqualityInfo(string operandText, string literal, string comparisonKey)
        {
            OperandText = operandText;
            Literal = literal;
            ComparisonKey = comparisonKey;
        }

        internal string OperandText { get; }
        internal string Literal { get; }
        internal string ComparisonKey { get; }

        public bool Equals(StringEqualityInfo other) =>
            string.Equals(OperandText, other.OperandText, StringComparison.Ordinal)
            && string.Equals(Literal, other.Literal, StringComparison.Ordinal)
            && string.Equals(ComparisonKey, other.ComparisonKey, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is StringEqualityInfo other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (OperandText is null ? 0 : StringComparer.Ordinal.GetHashCode(OperandText));
                hash = (hash * 31) + (Literal is null ? 0 : StringComparer.Ordinal.GetHashCode(Literal));
                hash = (hash * 31) + (ComparisonKey is null ? 0 : StringComparer.Ordinal.GetHashCode(ComparisonKey));
                return hash;
            }
        }

        public override string ToString() => $"({OperandText}, {Literal}, {ComparisonKey})";
    }
}
