using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128249: <c lang="csharp">Array.Resize</c> or <c lang="csharp">Enumerable.Concat</c>
///     regrows the same array on every loop iteration.
/// </summary>
/// <remarks>
///     Each iteration reallocates the array and copies every element, so the loop costs quadratic
///     time and allocation. The rule reports a <c lang="csharp">Resize</c> call in a loop body and
///     a <c lang="csharp">Concat</c> whose sequence is assigned back to the same variable. A call
///     outside a loop, a lambda body, and a <c lang="csharp">Concat</c> assigned to a different
///     variable stay silent. The rule carries no code fix, because a presized array or a builder
///     depends on the loop.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArrayGrowthInLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128249";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Array regrown inside a loop",
        "'{0}' reallocates on every loop iteration",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "Presize the array, use a builder, or collect the items first. Regrowing the array on every iteration copies every element.");

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
            var arrayType = start.Compilation.GetSpecialType(SpecialType.System_Array);
            var enumerableType = start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, arrayType, enumerableType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol arrayType,
        INamedTypeSymbol? enumerableType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (LoopContext.FindEnclosingLoopBody(invocation) is null
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method)
        {
            return;
        }

        if (IsArrayResize(method, arrayType))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), method.Name));
            return;
        }

        if (!IsSameVariableConcat(invocation, method, enumerableType, context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), method.Name));
    }

    private static bool IsArrayResize(IMethodSymbol method, INamedTypeSymbol arrayType)
    {
        return method.IsStatic
            && string.Equals(method.Name, "Resize", StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, arrayType);
    }

    /// <summary>
    ///     True when the <c lang="csharp">Concat</c> sequence is assigned back to the variable it
    ///     reads, as in <c lang="csharp">items = items.Concat(extra).ToArray()</c>.
    /// </summary>
    private static bool IsSameVariableConcat(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        INamedTypeSymbol? enumerableType,
        SyntaxNodeAnalysisContext context)
    {
        if (enumerableType is null
            || !string.Equals(method.Name, "Concat", StringComparison.Ordinal))
        {
            return false;
        }

        var definingType = method.ReducedFrom?.ContainingType ?? method.ContainingType;
        if (!SymbolEqualityComparer.Default.Equals(definingType, enumerableType))
        {
            return false;
        }

        if (invocation.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault() is not { } assignment
            || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            return false;
        }

        var target = context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol;
        var source = GetSequenceSymbol(invocation, method, context);
        return target is not null
            && source is not null
            && SymbolEqualityComparer.Default.Equals(target, source);
    }

    private static ISymbol? GetSequenceSymbol(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SyntaxNodeAnalysisContext context)
    {
        var expression = GetSequenceExpression(invocation, method);
        return expression is null
            ? null
            : context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol;
    }

    private static ExpressionSyntax? GetSequenceExpression(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (method.ReducedFrom is not null)
        {
            return invocation.Expression is MemberAccessExpressionSyntax access ? access.Expression : null;
        }

        return invocation.ArgumentList.Arguments.Count > 0
            ? invocation.ArgumentList.Arguments[0].Expression
            : null;
    }
}
