using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
/// E128048: Flags <c>if/else-if</c> chains (3+ branches) that compare against enum values.
/// Use a <see langword="switch"/> statement or expression instead — it provides exhaustiveness
/// checking via IDE0072/SS018 and is more maintainable.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EnumIfElseChainAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128048";

    private const int MinimumBranchThreshold = 3;

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Use switch instead of if/else-if chain on enum values",
        messageFormat: "if/else-if chain on enum type '{0}' with {1} branches — use a switch statement or expression instead",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "if/else-if chains on enum values bypass the compiler's exhaustiveness checking. " +
            "A switch statement enables IDE0072, SS018, and S125 to catch unhandled enum members at build time.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeIfStatement, SyntaxKind.IfStatement);
    }

    private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context)
    {
        var ifStatement = (IfStatementSyntax)context.Node;

        // Only analyze the outermost if — skip else-if children
        if (ifStatement.Parent is ElseClauseSyntax)
        {
            return;
        }

        var (branchCount, enumTypeName) = CountEnumBranches(ifStatement, context.SemanticModel, context.CancellationToken);

        if (branchCount < MinimumBranchThreshold || enumTypeName is null)
        {
            return;
        }

        // Span the diagnostic across the entire if/else-if chain
        var lastElse = GetLastElseIf(ifStatement);
        var span = Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(
            ifStatement.SpanStart,
            lastElse?.Span.End ?? ifStatement.Span.End);
        var location = Location.Create(ifStatement.SyntaxTree, span);

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, enumTypeName, branchCount));
    }

    private static (int BranchCount, string? EnumTypeName) CountEnumBranches(
        IfStatementSyntax ifStatement,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        var count = 0;
        string? enumTypeName = null;
        var current = ifStatement;

        while (current is not null)
        {
            var typeName = GetEnumComparisonTypeName(current.Condition, semanticModel, cancellationToken);
            if (typeName is null)
            {
                return (0, null);
            }

            if (enumTypeName is null)
            {
                enumTypeName = typeName;
            }
            else if (!string.Equals(enumTypeName, typeName, StringComparison.Ordinal))
            {
                return (0, null);
            }

            count++;
            current = current.Else?.Statement as IfStatementSyntax;
        }

        return (count, enumTypeName);
    }

    private static string? GetEnumComparisonTypeName(
        ExpressionSyntax condition,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (condition is not BinaryExpressionSyntax binary)
        {
            return null;
        }

        if (!binary.IsKind(SyntaxKind.EqualsExpression) && !binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return null;
        }

        var leftType = semanticModel.GetTypeInfo(binary.Left, cancellationToken).Type;
        if (leftType is { TypeKind: TypeKind.Enum })
        {
            return leftType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        var rightType = semanticModel.GetTypeInfo(binary.Right, cancellationToken).Type;
        return rightType is { TypeKind: TypeKind.Enum } ? rightType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) : null;
    }

    private static IfStatementSyntax? GetLastElseIf(IfStatementSyntax ifStatement)
    {
        IfStatementSyntax? last = null;
        var current = ifStatement.Else?.Statement as IfStatementSyntax;
        while (current is not null)
        {
            last = current;
            current = current.Else?.Statement as IfStatementSyntax;
        }

        return last;
    }
}
