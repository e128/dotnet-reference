using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImmutableArrayCreateToArrayAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128083";

    private const string ImmutableArrayMetadataName = "System.Collections.Immutable.ImmutableArray";
    private const string ImmutableCollectionsMarshalMetadataName = "System.Runtime.InteropServices.ImmutableCollectionsMarshal";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use ImmutableCollectionsMarshal.AsImmutableArray instead of ImmutableArray.Create/CreateRange with ToArray",
        "'{0}' copies the array; use 'ImmutableCollectionsMarshal.AsImmutableArray' for zero-copy wrapping",
        "Performance",
        DiagnosticSeverity.Warning,
        true,
        "ImmutableArray.Create(x.ToArray()) and ImmutableArray.CreateRange(x.ToArray()) each copy the array. " +
        "ImmutableCollectionsMarshal.AsImmutableArray wraps an existing array without copying.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var marshalType = compilationContext.Compilation.GetTypeByMetadataName(ImmutableCollectionsMarshalMetadataName);
            if (marshalType is null)
            {
                return;
            }

            var immutableArrayType = compilationContext.Compilation.GetTypeByMetadataName(ImmutableArrayMetadataName);
            if (immutableArrayType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, immutableArrayType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol immutableArrayType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, immutableArrayType))
        {
            return;
        }

        if (methodSymbol.Name is not ("Create" or "CreateRange"))
        {
            return;
        }

        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        var argument = invocation.ArgumentList.Arguments[0].Expression;
        if (argument is not InvocationExpressionSyntax innerInvocation)
        {
            return;
        }

        if (innerInvocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.Text, "ToArray", StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            $"ImmutableArray.{methodSymbol.Name}"));
    }
}
