using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Design;

/// <summary>
///     E128058: Detects methods that return a <c>List&lt;T&gt;</c> field directly as
///     <c>IReadOnlyList&lt;T&gt;</c> or <c>IReadOnlyCollection&lt;T&gt;</c>, exposing the
///     internal mutable list through the read-only interface. The caller can cast it back to
///     <c>List&lt;T&gt;</c> and mutate it. Use <c>.AsReadOnly()</c> or wrap in a
///     <c>ReadOnlyCollection&lt;T&gt;</c> instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ListAsReadOnlyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128058";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Return List<T> via .AsReadOnly() when exposing as IReadOnlyList<T>",
        "Returning a mutable List<T> as IReadOnlyList<T> leaks the internal list — use .AsReadOnly() to prevent callers from casting back to List<T>",
        "Design",
        DiagnosticSeverity.Warning,
        true,
        "Returning a mutable List<T> directly as IReadOnlyList<T> allows callers to cast back to List<T> and mutate the internal collection. Return .AsReadOnly() instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeReturn, OperationKind.Return);
    }

    private static void AnalyzeReturn(OperationAnalysisContext context)
    {
        var returnOp = (IReturnOperation)context.Operation;
        if (returnOp.ReturnedValue is null)
        {
            return;
        }

        // Strip implicit conversions to get the actual underlying type.
        // An implicit List<T> → IReadOnlyList<T> conversion wraps the field reference,
        // making ReturnedValue.Type = IReadOnlyList<T> instead of List<T>.
        var returnedValue = returnOp.ReturnedValue;
        while (returnedValue is IConversionOperation { IsImplicit: true } conv)
        {
            returnedValue = conv.Operand;
        }

        var returnedType = returnedValue.Type;
        if (returnedType is null || !IsListType(returnedType))
        {
            return;
        }

        // The declared return type of the containing method or property
        var expectedReturn = GetContainingReturnType(context.ContainingSymbol);
        if (expectedReturn is null || !IsReadOnlyListInterface(expectedReturn))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, returnedValue.Syntax.GetLocation()));
    }

    private static ITypeSymbol? GetContainingReturnType(ISymbol symbol)
    {
        return symbol is IMethodSymbol method ? method.ReturnType : symbol is IPropertySymbol property ? property.Type : null;
    }

    private static bool IsListType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType
               && string.Equals(namedType.Name, "List", StringComparison.Ordinal)
               && IsInSystemCollectionsGeneric(namedType.ContainingNamespace);
    }

    private static bool IsReadOnlyListInterface(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType
               && (string.Equals(namedType.Name, "IReadOnlyList", StringComparison.Ordinal)
                   || string.Equals(namedType.Name, "IReadOnlyCollection", StringComparison.Ordinal))
               && IsInSystemCollectionsGeneric(namedType.ContainingNamespace);
    }

    private static bool IsInSystemCollectionsGeneric(INamespaceSymbol? ns)
    {
        return ns is not null
               && string.Equals(ns.Name, "Generic", StringComparison.Ordinal)
               && ns.ContainingNamespace is { } parent
               && string.Equals(parent.Name, "Collections", StringComparison.Ordinal)
               && parent.ContainingNamespace is { } grandparent
               && string.Equals(grandparent.Name, "System", StringComparison.Ordinal);
    }
}
