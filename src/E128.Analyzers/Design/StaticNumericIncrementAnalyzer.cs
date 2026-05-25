using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaticNumericIncrementAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128087";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Static numeric field should not be incremented with ++/--",
        "Static field '{0}' is mutated with {1} — use Interlocked or remove the 'static' keyword",
        "Design",
        DiagnosticSeverity.Warning,
        true,
        "Static numeric fields mutated across invocations are a concurrency smell. The operation is not atomic, " +
        "and the value leaks across call contexts. Use Interlocked methods (for int/long) or remove 'static'.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzePrefixUnary, SyntaxKind.PreIncrementExpression);
        context.RegisterSyntaxNodeAction(AnalyzePrefixUnary, SyntaxKind.PreDecrementExpression);
        context.RegisterSyntaxNodeAction(AnalyzePostfixUnary, SyntaxKind.PostIncrementExpression);
        context.RegisterSyntaxNodeAction(AnalyzePostfixUnary, SyntaxKind.PostDecrementExpression);
        context.RegisterSyntaxNodeAction(AnalyzeCompoundAssignment, SyntaxKind.AddAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeCompoundAssignment, SyntaxKind.SubtractAssignmentExpression);
    }

    private static void AnalyzePrefixUnary(SyntaxNodeAnalysisContext context)
    {
        var unary = (PrefixUnaryExpressionSyntax)context.Node;
        CheckStaticNumericMutation(context, unary.Operand, unary.OperatorToken);
    }

    private static void AnalyzePostfixUnary(SyntaxNodeAnalysisContext context)
    {
        var postfix = (PostfixUnaryExpressionSyntax)context.Node;
        CheckStaticNumericMutation(context, postfix.Operand, postfix.OperatorToken);
    }

    private static void AnalyzeCompoundAssignment(SyntaxNodeAnalysisContext context)
    {
        var compound = (AssignmentExpressionSyntax)context.Node;
        CheckStaticNumericMutation(context, compound.Left, compound.OperatorToken);
    }

    private static void CheckStaticNumericMutation(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax operand,
        SyntaxToken operatorToken)
    {
        var symbolInfo = context.SemanticModel.GetSymbolInfo(operand, context.CancellationToken);

        if (symbolInfo.Symbol is not IFieldSymbol fieldSymbol)
        {
            return;
        }

        if (!fieldSymbol.IsStatic || fieldSymbol.IsReadOnly || fieldSymbol.IsVolatile)
        {
            return;
        }

        if (!IsSupportedNumericType(fieldSymbol.Type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            operatorToken.GetLocation(),
            fieldSymbol.Name,
            operatorToken.ValueText));
    }

    private static bool IsSupportedNumericType(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64
            or SpecialType.System_Single or SpecialType.System_Double;
    }
}
