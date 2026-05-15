using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ByteSizeUnwrapViaCastAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128082";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not unwrap ByteSize via cast",
        "Cast to '{0}' unwraps ByteSize.{1}, defeating type safety. Use ByteSize comparison operators or formatting instead.",
        "Design",
        DiagnosticSeverity.Warning,
        true,
        "Casting a ByteSize unit property (Bytes, Kilobytes, etc.) to a numeric type defeats the type safety " +
        "introduced by using ByteSize. Use ByteSize comparison operators (<, >, ==) for comparisons and " +
        "ByteSize.ToString() for formatting.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCastExpression, SyntaxKind.CastExpression);
    }

    private static void AnalyzeCastExpression(SyntaxNodeAnalysisContext context)
    {
        var castExpression = (CastExpressionSyntax)context.Node;

        var inner = UnwrapParentheses(castExpression.Expression);

        if (inner is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var memberName = memberAccess.Name.Identifier.Text;
        if (!IsUnitProperty(memberName))
        {
            return;
        }

        if (!IsTargetNumericType(context.SemanticModel, castExpression.Type, context.CancellationToken))
        {
            return;
        }

        if (!IsAccessOnByteSizeType(context.SemanticModel, memberAccess, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            castExpression.GetLocation(),
            castExpression.Type.ToString(),
            memberName));
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parens)
        {
            expression = parens.Expression;
        }

        return expression;
    }

    private static bool IsUnitProperty(string name)
    {
        return string.Equals(name, "Bytes", StringComparison.Ordinal)
               || string.Equals(name, "Bits", StringComparison.Ordinal)
               || string.Equals(name, "Kilobytes", StringComparison.Ordinal)
               || string.Equals(name, "Megabytes", StringComparison.Ordinal)
               || string.Equals(name, "Gigabytes", StringComparison.Ordinal)
               || string.Equals(name, "Terabytes", StringComparison.Ordinal);
    }

    private static bool IsTargetNumericType(
        SemanticModel model,
        TypeSyntax typeSyntax,
        CancellationToken cancellationToken)
    {
        var typeInfo = model.GetTypeInfo(typeSyntax, cancellationToken);
        return typeInfo.Type is not null && IsNumericType(typeInfo.Type);
    }

    private static bool IsAccessOnByteSizeType(
        SemanticModel model,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        var symbolInfo = model.GetSymbolInfo(memberAccess, cancellationToken);
        return symbolInfo.Symbol is IPropertySymbol property && IsByteSizeType(property.ContainingType);
    }

    private static bool IsNumericType(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_Int32
            or SpecialType.System_Int64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal;
    }

    private static bool IsByteSizeType(ITypeSymbol? type)
    {
        return type is not null
               && string.Equals(type.Name, "ByteSize", StringComparison.Ordinal)
               && string.Equals(type.ContainingNamespace?.ToString(), "Pug.Core.Classes", StringComparison.Ordinal);
    }
}
