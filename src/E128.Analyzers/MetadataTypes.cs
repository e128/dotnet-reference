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
}
