using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128221: Reports a <c lang="csharp">Value</c> read or a case-type cast on a union
///     outside a pattern match. A <c lang="csharp">Value</c> read boxes the case and skips
///     the case set, and a cast trusts the runtime state instead of the type system. A case
///     pattern reads the value without boxing and stays checked against the case list. A
///     compiler-declared union exposes no <c lang="csharp">TryGetValue</c> member.
/// </summary>
/// <remarks>
///     No code fix is provided — converting a read or a cast into a pattern changes control
///     flow, which cannot be automated safely.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnionValueReadAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128221";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Union case read outside a pattern match",
        "Read the union case through a pattern instead of a Value read or cast. The read skips the case set and boxes the value.",
        "E128-Design",
        DiagnosticSeverity.Warning,
        true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeCast, SyntaxKind.CastExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        if (!string.Equals(memberAccess.Name.Identifier.Text, "Value", StringComparison.Ordinal))
        {
            return;
        }

        var rawType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        if (rawType is null)
        {
            return;
        }

        var receiverType = TypeSymbols.UnwrapNullable(rawType);
        if (!UnionTypes.IsUnion(receiverType))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        if (symbol is not IPropertySymbol { Name: { } name } member
            || !string.Equals(name, "Value", StringComparison.Ordinal)
            || !SymbolEqualityComparer.Default.Equals(
                (receiverType as INamedTypeSymbol)?.OriginalDefinition,
                member.ContainingType?.OriginalDefinition))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation()));
    }

    private static void AnalyzeCast(SyntaxNodeAnalysisContext context)
    {
        var cast = (CastExpressionSyntax)context.Node;
        var operandType = context.SemanticModel.GetTypeInfo(cast.Expression, context.CancellationToken).Type;
        if (!UnionTypes.IsUnion(operandType))
        {
            return;
        }

        var targetType = context.SemanticModel.GetTypeInfo(cast, context.CancellationToken).Type;
        if (targetType is null
            || SymbolEqualityComparer.Default.Equals(targetType, operandType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, cast.GetLocation()));
    }
}
