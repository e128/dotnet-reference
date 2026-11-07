using System;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers.Design;

/// <summary>
///     Recognises the project's ByteSize type. The data-size rules share the probe.
/// </summary>
internal static class ByteSizeType
{
    /// <summary>
    ///     True when the type is <c lang="csharp">Pug.Core.Classes.ByteSize</c>.
    /// </summary>
    internal static bool Is(ITypeSymbol? type)
    {
        return type is not null
               && string.Equals(type.Name, "ByteSize", StringComparison.Ordinal)
               && string.Equals(type.ContainingNamespace?.ToString(), "Pug.Core.Classes", StringComparison.Ordinal);
    }
}
