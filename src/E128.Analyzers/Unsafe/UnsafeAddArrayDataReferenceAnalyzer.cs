using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Unsafe;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsafeAddArrayDataReferenceAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128114";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Index the array instead of bypassing its bounds check",
        "Unsafe.Add over MemoryMarshal.GetArrayDataReference removes the bounds check. Use an indexer and let the JIT elide the check.",
        "Unsafe",
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
        context.RegisterCompilationStartAction(start =>
        {
            var unsafeType = MetadataTypes.Find(start.Compilation, "System.Runtime.CompilerServices.Unsafe");
            if (unsafeType.IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, unsafeType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> unsafeType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (InvocationNames.GetSimpleName(invocation) is not { } memberAccess
            || !string.Equals(memberAccess.Identifier.ValueText, "Add", StringComparison.Ordinal))
        {
            return;
        }

        if (TryGetIndexedAccess(invocation) is null
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !MetadataTypes.Contains(unsafeType, method.ContainingType))
        {
            return;
        }

        // Only the generic T[] overload maps to arr[i]. The Array overload returns ref byte and uses byte offsets.
        var reference = (InvocationExpressionSyntax)invocation.ArgumentList.Arguments[0].Expression;
        if (context.SemanticModel.GetSymbolInfo(reference, context.CancellationToken).Symbol is not IMethodSymbol { IsGenericMethod: true } inner
            || !string.Equals(inner.ContainingType.Name, "MemoryMarshal", StringComparison.Ordinal)
            || inner.Parameters.Length != 1
            || inner.Parameters[0].Type is not IArrayTypeSymbol { IsSZArray: true })
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    internal static (ExpressionSyntax Array, ExpressionSyntax Index)? TryGetIndexedAccess(
        InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 2)
        {
            return null;
        }

        var first = invocation.ArgumentList.Arguments[0];

        return first.RefKindKeyword.ValueText.Length > 0
            && first.Expression is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetArrayDataReference" },
                ArgumentList.Arguments.Count: 1,
            } reference
            ? (reference.ArgumentList.Arguments[0].Expression, invocation.ArgumentList.Arguments[1].Expression)
            : null;
    }
}
