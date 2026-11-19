using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128238: An array comparison uses reference equality. <c lang="csharp">==</c>,
///     <c lang="csharp">!=</c>, and <c lang="csharp">Equals</c> compare the array
///     identity, not the elements.
/// </summary>
/// <remarks>
///     The rule reports single-rank arrays only. A multi-dimensional array implements no
///     generic <c lang="csharp">IEnumerable&lt;T&gt;</c>, so the sequence-equality fix cannot
///     repair that shape. A <c lang="csharp">byte[]</c> comparison stays silent, because a
///     reference check against a sentinel buffer is a common deliberate pattern. The null
///     forms also stay silent, because a null test needs reference equality.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArrayReferenceComparisonAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128238";

    private const int StaticEqualsArgumentCount = 2;

    private const int InstanceEqualsArgumentCount = 1;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Array compared by reference",
        "Use element equality for an array comparison, because this operator compares references",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "SequenceEqual compares the elements. Reference equality reports false for two arrays with equal contents.");

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

        context.RegisterCompilationStartAction(static start =>
        {
            var objectType = start.Compilation.GetSpecialType(SpecialType.System_Object);
            start.RegisterSyntaxNodeAction(AnalyzeBinary, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, objectType), SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeBinary(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (IsNullConstant(binary.Left) || IsNullConstant(binary.Right))
        {
            return;
        }

        if (!IsComparableArray(binary.Left, context) || !IsComparableArray(binary.Right, context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, binary.GetLocation()));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol objectType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !string.Equals(method.Name, "Equals", StringComparison.Ordinal)
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, objectType))
        {
            return;
        }

        var matched = method.IsStatic
            ? MatchesStaticEquals(invocation, context)
            : MatchesInstanceEquals(invocation, context);
        if (!matched)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool MatchesStaticEquals(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        return arguments.Count is StaticEqualsArgumentCount
            && IsComparableArray(arguments[0].Expression, context)
            && IsComparableArray(arguments[1].Expression, context);
    }

    private static bool MatchesInstanceEquals(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        return arguments.Count is InstanceEqualsArgumentCount
            && invocation.Expression is MemberAccessExpressionSyntax access
            && IsComparableArray(access.Expression, context)
            && IsComparableArray(arguments[0].Expression, context);
    }

    /// <summary>
    ///     True for a single-rank array that the sequence-equality fix can handle. A byte array,
    ///     a pointer element, and a function-pointer element stay out.
    /// </summary>
    private static bool IsComparableArray(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
    {
        return context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type is IArrayTypeSymbol
        {
            Rank: 1
        } array && array.ElementType.SpecialType is not SpecialType.System_Byte
            && array.ElementType.TypeKind is not (TypeKind.Pointer or TypeKind.FunctionPointer);
    }

    /// <summary>
    ///     True for a null literal, the default literal, and a <c lang="csharp">default(T)</c>
    ///     expression. A null test needs reference equality, so the rule stays silent.
    /// </summary>
    private static bool IsNullConstant(ExpressionSyntax expression)
    {
        return expression.IsKind(SyntaxKind.NullLiteralExpression)
            || expression.IsKind(SyntaxKind.DefaultLiteralExpression)
            || expression.IsKind(SyntaxKind.DefaultExpression);
    }
}
