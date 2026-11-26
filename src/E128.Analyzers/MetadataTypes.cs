using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers;

internal static class MetadataTypes
{
    internal const string Unsafe = "System.Runtime.CompilerServices.Unsafe";

    /// <summary>
    ///     Returns every type with the metadata name. GetTypeByMetadataName returns null when two
    ///     referenced assemblies define the same name, which would silently turn a rule off.
    /// </summary>
    internal static ImmutableArray<INamedTypeSymbol> Find(Compilation compilation, string metadataName)
    {
        return compilation.GetTypesByMetadataName(metadataName);
    }

    internal static bool Contains(ImmutableArray<INamedTypeSymbol> types, ISymbol? candidate)
    {
        return candidate is not null && types.Any(type => SymbolEqualityComparer.Default.Equals(type, candidate));
    }

    /// <summary>
    ///     Registers the syntax action once per type that carries the metadata name. An ambiguous
    ///     name registers every match, so the rule still runs. A report cannot duplicate, because
    ///     each action compares the bound symbol to one type.
    /// </summary>
    internal static void RegisterForType(
        CompilationStartAnalysisContext context,
        string metadataName,
        Action<SyntaxNodeAnalysisContext, INamedTypeSymbol> action,
        params SyntaxKind[] syntaxKinds)
    {
        foreach (var type in Find(context.Compilation, metadataName))
        {
            context.RegisterSyntaxNodeAction(
                nodeContext => action(nodeContext, type),
                syntaxKinds);
        }
    }

    /// <summary>
    ///     Registers one syntax action with every type whose metadata name resolves, and returns
    ///     true. Returns false and registers nothing when the name has no match, so the caller
    ///     bails as one decision.
    /// </summary>
    internal static bool TryRegisterForTypes(
        CompilationStartAnalysisContext context,
        string metadataName,
        Action<SyntaxNodeAnalysisContext, ImmutableArray<INamedTypeSymbol>> action,
        params SyntaxKind[] syntaxKinds)
    {
        return TryRegisterForTypes(context, [metadataName], action, syntaxKinds);
    }

    /// <summary>
    ///     Registers one syntax action with all types whose metadata names resolve, and returns
    ///     true. Returns false and registers nothing when any name has no match, so a rule that
    ///     needs several types skips them together.
    /// </summary>
    internal static bool TryRegisterForTypes(
        CompilationStartAnalysisContext context,
        string[] metadataNames,
        Action<SyntaxNodeAnalysisContext, ImmutableArray<INamedTypeSymbol>> action,
        params SyntaxKind[] syntaxKinds)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var metadataName in metadataNames)
        {
            var types = Find(context.Compilation, metadataName);
            if (types.IsEmpty)
            {
                return false;
            }

            builder.AddRange(types);
        }

        var resolved = builder.ToImmutable();
        context.RegisterSyntaxNodeAction(nodeContext => action(nodeContext, resolved), syntaxKinds);
        return true;
    }
}
