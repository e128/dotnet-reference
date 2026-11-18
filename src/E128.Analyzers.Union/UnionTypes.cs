using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

/// <summary>
///     Recognizes the C# union forms. A source-declared union confirms its shape through the
///     typed <see cref="UnionDeclarationSyntax" /> node, which sits behind RSEXPERIMENTAL006
///     at this Roslyn band; the two Roslyn component projects take that suppression, and the
///     maintainer approved it for the union rule family. A binary union, or a hand-written
///     type that marks itself the same way, carries the compiler union interface, so the
///     interface check covers those shapes.
/// </summary>
internal static class UnionTypes
{
    /// <summary>
    ///     True when <paramref name="type" /> is a union: a union declaration in source, or a
    ///     type that carries the compiler union interface.
    /// </summary>
    internal static bool IsUnion(ITypeSymbol? type)
    {
        if (type is null
            || type.TypeKind is not (TypeKind.Struct or TypeKind.Class))
        {
            return false;
        }

        if (HasUnionDeclarationSyntax(type))
        {
            return true;
        }

        return type.AllInterfaces.Any(IsCompilerUnionInterface);
    }

    private static bool HasUnionDeclarationSyntax(ITypeSymbol type)
    {
        return type.DeclaringSyntaxReferences.Any(static reference =>
            reference.GetSyntax(CancellationToken.None) is UnionDeclarationSyntax);
    }

    private static bool IsCompilerUnionInterface(ITypeSymbol interfaceType)
    {
        // The compiler synthesizes the interface, so no metadata name resolves it. Match the
        // name and the namespace chain instead, which the checklist allows when no symbol exists.
        if (!string.Equals(interfaceType.Name, "IUnion", StringComparison.Ordinal))
        {
            return false;
        }

        var services = interfaceType.ContainingNamespace;
        if (services is null || !string.Equals(services.Name, "CompilerServices", StringComparison.Ordinal))
        {
            return false;
        }

        var runtime = services.ContainingNamespace;
        if (runtime is null || !string.Equals(runtime.Name, "Runtime", StringComparison.Ordinal))
        {
            return false;
        }

        var system = runtime.ContainingNamespace;
        if (system is null || !string.Equals(system.Name, "System", StringComparison.Ordinal))
        {
            return false;
        }

        return system.ContainingNamespace is { IsGlobalNamespace: true };
    }
}
