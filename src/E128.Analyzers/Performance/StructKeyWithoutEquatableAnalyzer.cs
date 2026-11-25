using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128253: Reports a <c lang="csharp">Dictionary&lt;TKey, TValue&gt;</c>,
///     <c lang="csharp">HashSet&lt;T&gt;</c>, or
///     <c lang="csharp">ConcurrentDictionary&lt;TKey, TValue&gt;</c> created with the default
///     equality comparer where the key is a source struct without
///     <c lang="csharp">IEquatable&lt;T&gt;</c>.
/// </summary>
/// <remarks>
///     <c lang="csharp">EqualityComparer&lt;T&gt;.Default</c> falls back to an object comparer for
///     such a key, so every hash and equality test boxes the key. The report stays silent when the
///     key comes from metadata, implements <c lang="csharp">IEquatable&lt;T&gt;</c>, is a value tuple,
///     or the creation passes an <c lang="csharp">IEqualityComparer&lt;T&gt;</c> argument. A collection expression
///     that fills a set reaches no object creation, so that form is outside the rule. The rule
///     carries no fix, because the remedy adds a member to the key type.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StructKeyWithoutEquatableAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128253";

    private const string DictionaryMetadataName = "System.Collections.Generic.Dictionary`2";

    private const string HashSetMetadataName = "System.Collections.Generic.HashSet`1";

    private const string ConcurrentDictionaryMetadataName = "System.Collections.Concurrent.ConcurrentDictionary`2";

    private const string EquatableMetadataName = "System.IEquatable`1";

    private const string EqualityComparerMetadataName = "System.Collections.Generic.IEqualityComparer`1";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Struct dictionary key does not implement IEquatable<T>",
        "Struct '{0}' does not implement IEquatable<{0}>, so every lookup boxes the key",
        "E128-Performance",
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
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var equatable = context.Compilation.GetTypeByMetadataName(EquatableMetadataName);
        var equalityComparer = context.Compilation.GetTypeByMetadataName(EqualityComparerMetadataName);

        if (equatable is null || equalityComparer is null)
        {
            return;
        }

        var dictionary = context.Compilation.GetTypeByMetadataName(DictionaryMetadataName);
        var hashSet = context.Compilation.GetTypeByMetadataName(HashSetMetadataName);
        var concurrentDictionary = context.Compilation.GetTypeByMetadataName(ConcurrentDictionaryMetadataName);

        context.RegisterOperationAction(
            operationContext => Analyze(
                operationContext,
                dictionary,
                hashSet,
                concurrentDictionary,
                equatable,
                equalityComparer),
            OperationKind.ObjectCreation);
    }

    private static void Analyze(
        OperationAnalysisContext context,
        INamedTypeSymbol? dictionary,
        INamedTypeSymbol? hashSet,
        INamedTypeSymbol? concurrentDictionary,
        INamedTypeSymbol equatable,
        INamedTypeSymbol equalityComparer)
    {
        var creation = (IObjectCreationOperation)context.Operation;

        if (creation.Type is not INamedTypeSymbol created
            || created.TypeArguments.IsEmpty
            || (!TypeSymbols.MatchesOriginalDefinition(created, dictionary)
                && !TypeSymbols.MatchesOriginalDefinition(created, hashSet)
                && !TypeSymbols.MatchesOriginalDefinition(created, concurrentDictionary)))
        {
            return;
        }

        var key = created.TypeArguments[0];

        if (!IsReportableKey(creation, key, equatable, equalityComparer))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            creation.Syntax.GetLocation(),
            key.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static bool IsReportableKey(
        IObjectCreationOperation creation,
        ITypeSymbol key,
        INamedTypeSymbol equatable,
        INamedTypeSymbol equalityComparer)
    {
        return key.TypeKind is TypeKind.Struct
            && key is not INamedTypeSymbol { IsTupleType: true }
            && !key.DeclaringSyntaxReferences.IsEmpty
            && !ImplementsEquatable(key, equatable)
            && !HasComparerArgument(creation, key, equalityComparer);
    }

    private static bool ImplementsEquatable(ITypeSymbol key, INamedTypeSymbol equatable)
    {
        foreach (var implemented in key.AllInterfaces)
        {
            if (TypeSymbols.MatchesOriginalDefinition(implemented, equatable)
                && implemented.TypeArguments.Length is 1
                && SymbolEqualityComparer.Default.Equals(implemented.TypeArguments[0], key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasComparerArgument(
        IObjectCreationOperation creation,
        ITypeSymbol key,
        INamedTypeSymbol equalityComparer)
    {
        foreach (var argument in creation.Arguments)
        {
            if (argument.Value.Type is { } argumentType
                && ImplementsEqualityComparer(argumentType, key, equalityComparer))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImplementsEqualityComparer(
        ITypeSymbol type,
        ITypeSymbol key,
        INamedTypeSymbol equalityComparer)
    {
        if (TypeSymbols.MatchesOriginalDefinition(type, equalityComparer)
            && type is INamedTypeSymbol { TypeArguments.Length: 1 } direct
            && SymbolEqualityComparer.Default.Equals(direct.TypeArguments[0], key))
        {
            return true;
        }

        foreach (var implemented in type.AllInterfaces)
        {
            if (TypeSymbols.MatchesOriginalDefinition(implemented, equalityComparer)
                && implemented.TypeArguments.Length is 1
                && SymbolEqualityComparer.Default.Equals(implemented.TypeArguments[0], key))
            {
                return true;
            }
        }

        return false;
    }
}
