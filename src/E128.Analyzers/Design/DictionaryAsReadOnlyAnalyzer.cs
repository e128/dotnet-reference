using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Design;

/// <summary>
///     E128060: Detects methods and properties that return a <c>Dictionary&lt;K,V&gt;</c> directly
///     as <c>IReadOnlyDictionary&lt;K,V&gt;</c>, exposing the internal mutable dictionary through the
///     read-only interface. The caller can cast it back to <c>Dictionary&lt;K,V&gt;</c> and mutate it.
///     Use <c>.AsReadOnly()</c> (requires .NET 9+) or wrap in a <c>ReadOnlyDictionary&lt;K,V&gt;</c>
///     instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DictionaryAsReadOnlyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128060";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Return Dictionary<K,V> via .AsReadOnly() when exposing as IReadOnlyDictionary<K,V>",
        "Returning a mutable Dictionary<K,V> as IReadOnlyDictionary<K,V> leaks the internal dictionary — use .AsReadOnly() to prevent callers from casting back to Dictionary<K,V>",
        "Design",
        DiagnosticSeverity.Warning,
        true,
        "Returning a mutable Dictionary<K,V> directly as IReadOnlyDictionary<K,V> allows callers to cast back to Dictionary<K,V> and mutate the internal collection. Return .AsReadOnly() instead.");

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
        // An implicit Dictionary<K,V> → IReadOnlyDictionary<K,V> conversion wraps the expression,
        // making ReturnedValue.Type = IReadOnlyDictionary<K,V> instead of Dictionary<K,V>.
        var returnedValue = returnOp.ReturnedValue;
        while (returnedValue is IConversionOperation { IsImplicit: true } conv)
        {
            returnedValue = conv.Operand;
        }

        var returnedType = returnedValue.Type;
        if (returnedType is null || !IsDictionaryType(returnedType))
        {
            return;
        }

        // The declared return type of the containing method or property.
        // Unwrap Task<T> and ValueTask<T> so this fires on async methods too.
        var expectedReturn = GetContainingReturnType(context.ContainingSymbol);
        if (expectedReturn is null || !IsReadOnlyDictionaryInterface(expectedReturn))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, returnedValue.Syntax.GetLocation()));
    }

    private static ITypeSymbol? GetContainingReturnType(ISymbol symbol)
    {
        var returnType = symbol is IMethodSymbol method ? method.ReturnType
            : symbol is IPropertySymbol property ? property.Type
            : null;

        // Unwrap Task<T> / ValueTask<T> so the rule fires on async methods.
        return returnType is INamedTypeSymbol { TypeArguments.Length: 1 } namedReturn
               && IsTaskOrValueTask(namedReturn)
            ? namedReturn.TypeArguments[0]
            : returnType;
    }

    private static bool IsTaskOrValueTask(INamedTypeSymbol type)
    {
        return (string.Equals(type.Name, "Task", StringComparison.Ordinal)
                || string.Equals(type.Name, "ValueTask", StringComparison.Ordinal))
               && type.ContainingNamespace is { } tasksNs
               && string.Equals(tasksNs.Name, "Tasks", StringComparison.Ordinal)
               && tasksNs.ContainingNamespace is { } threadingNs
               && string.Equals(threadingNs.Name, "Threading", StringComparison.Ordinal)
               && threadingNs.ContainingNamespace is { } systemNs
               && string.Equals(systemNs.Name, "System", StringComparison.Ordinal)
               && systemNs.ContainingNamespace is { IsGlobalNamespace: true };
    }

    private static bool IsDictionaryType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType
               && string.Equals(namedType.Name, "Dictionary", StringComparison.Ordinal)
               && IsInSystemCollectionsGeneric(namedType.ContainingNamespace);
    }

    private static bool IsReadOnlyDictionaryInterface(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType
               && string.Equals(namedType.Name, "IReadOnlyDictionary", StringComparison.Ordinal)
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
