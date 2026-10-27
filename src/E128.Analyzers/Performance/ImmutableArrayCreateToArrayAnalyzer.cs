using System;
using System.Collections.Generic;
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

    // BCL types whose ToArray always returns a new array.
    private static readonly HashSet<string> FreshArrayToArrayOwners = new(StringComparer.Ordinal)
    {
        "System.Linq.Enumerable",
        "System.Linq.ImmutableArrayExtensions",
        "System.Collections.Generic.List<T>",
        "System.Collections.Generic.HashSet<T>",
        "System.Collections.Generic.Queue<T>",
        "System.Collections.Generic.Stack<T>"
    };

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

        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };

        if (methodName is not ("Create" or "CreateRange"))
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

        // Bind once, after the syntax gates. Both gates reject only calls the symbol check also
        // rejects, so the diagnostic set does not change.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, immutableArrayType))
        {
            return;
        }

        // AsImmutableArray wraps the array without a copy. Only an allowlisted BCL ToArray guarantees a fresh array.
        if (context.SemanticModel.GetSymbolInfo(innerInvocation, context.CancellationToken).Symbol is not IMethodSymbol inner
            || !FreshArrayToArrayOwners.Contains(inner.ContainingType.OriginalDefinition.ToDisplayString()))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            $"ImmutableArray.{method.Name}"));
    }
}
