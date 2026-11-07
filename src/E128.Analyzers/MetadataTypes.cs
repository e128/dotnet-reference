using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

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

    /// <summary>
    ///     Registers the syntax action only when the compilation defines the metadata name. The
    ///     lookup uses <see cref="Compilation.GetTypeByMetadataName" />, so an ambiguous name turns
    ///     the rule off.
    /// </summary>
    internal static void RegisterForType(
        CompilationStartAnalysisContext context,
        string metadataName,
        Action<SyntaxNodeAnalysisContext, INamedTypeSymbol> action,
        params SyntaxKind[] syntaxKinds)
    {
        var type = context.Compilation.GetTypeByMetadataName(metadataName);
        if (type is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => action(nodeContext, type),
            syntaxKinds);
    }
}
