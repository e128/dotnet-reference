using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128027: Flags <c lang="csharp">static readonly HashSet&lt;T&gt;</c> and <c lang="csharp">static readonly Dictionary&lt;TKey, TValue&gt;</c>
///     fields that should use <c lang="csharp">FrozenSet&lt;T&gt;</c> / <c lang="csharp">FrozenDictionary&lt;TKey, TValue&gt;</c> for
///     optimized read performance on process-lifetime collections.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaticReadonlyFrozenCollectionAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128027";

    private const string HashSetMetadataName = "System.Collections.Generic.HashSet`1";
    private const string DictionaryMetadataName = "System.Collections.Generic.Dictionary`2";
    private const string FrozenSetMetadataName = "System.Collections.Frozen.FrozenSet`1";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use FrozenSet/FrozenDictionary for static readonly collections",
        "Static readonly '{0}' should be '{1}' for optimized read performance",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "Static readonly HashSet<T> and Dictionary<TKey, TValue> fields initialized at declaration " +
        "should use FrozenSet<T> / FrozenDictionary<TKey, TValue> from System.Collections.Frozen. " +
        "Frozen collections trade construction time for optimized read access — ideal for process-lifetime fields.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            // Guard: only register if FrozenSet<T> exists in the compilation (.NET 8+)
            var frozenSetType = compilationContext.Compilation.GetTypeByMetadataName(FrozenSetMetadataName);
            if (frozenSetType is null)
            {
                return;
            }

            var hashSetType = compilationContext.Compilation.GetTypeByMetadataName(HashSetMetadataName);
            var dictionaryType = compilationContext.Compilation.GetTypeByMetadataName(DictionaryMetadataName);

            if (hashSetType is null && dictionaryType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeFieldDeclaration(nodeContext, hashSetType, dictionaryType),
                SyntaxKind.FieldDeclaration);
        });
    }

    private static void AnalyzeFieldDeclaration(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? hashSetType,
        INamedTypeSymbol? dictionaryType)
    {
        var fieldDeclaration = (FieldDeclarationSyntax)context.Node;

        // One type node declares every variable in the list, so a frozen rewrite cannot cover one
        // variable alone. The fix rewrites the whole declaration, and the other variables would go.
        // The field must also have an initializer at the declaration site.
        if (fieldDeclaration.Declaration.Variables.Count is not 1
            || !HasStaticReadonlyModifiers(fieldDeclaration)
            || !HasDeclarationInitializer(fieldDeclaration))
        {
            return;
        }

        // Get the field symbol to check its type
        foreach (var variable in fieldDeclaration.Declaration.Variables)
        {
            if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken)
                is not IFieldSymbol { Type: INamedTypeSymbol { IsGenericType: true } fieldType }
                || GetSuggestedType(fieldType, hashSetType, dictionaryType) is not { } suggestedType)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                fieldDeclaration.Declaration.GetLocation(),
                fieldType.ToDisplayString(),
                suggestedType));
        }
    }

    private static string? GetSuggestedType(
        INamedTypeSymbol fieldType,
        INamedTypeSymbol? hashSetType,
        INamedTypeSymbol? dictionaryType)
    {
        var originalDefinition = fieldType.OriginalDefinition;

        if (hashSetType is not null && SymbolEqualityComparer.Default.Equals(originalDefinition, hashSetType))
        {
            return $"FrozenSet<{fieldType.TypeArguments[0].ToDisplayString()}>";
        }

        return dictionaryType is not null && SymbolEqualityComparer.Default.Equals(originalDefinition, dictionaryType)
            ? $"FrozenDictionary<{fieldType.TypeArguments[0].ToDisplayString()}, {fieldType.TypeArguments[1].ToDisplayString()}>"
            : null;
    }

    private static bool HasStaticReadonlyModifiers(FieldDeclarationSyntax fieldDeclaration)
    {
        var hasStatic = false;
        var hasReadonly = false;

        foreach (var modifier in fieldDeclaration.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.StaticKeyword))
            {
                hasStatic = true;
            }
            else if (modifier.IsKind(SyntaxKind.ReadOnlyKeyword))
            {
                hasReadonly = true;
            }
        }

        return hasStatic && hasReadonly;
    }

    private static bool HasDeclarationInitializer(FieldDeclarationSyntax fieldDeclaration)
    {
        foreach (var variable in fieldDeclaration.Declaration.Variables)
        {
            if (variable.Initializer is not null)
            {
                return true;
            }
        }

        return false;
    }
}
