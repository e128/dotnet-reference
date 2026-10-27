using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers;

internal static class MetadataTypes
{
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
}
