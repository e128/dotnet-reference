using System;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers.Design;

/// <summary>
///     Recognises the project's ByteSize type. The data-size rules share the probe.
/// </summary>
internal static class ByteSizeType
{
    /// <summary>
    ///     True when the type is named <c lang="csharp">ByteSize</c>, whatever its namespace.
    ///     A namespace move must not break the exemption or the cast rule.
    /// </summary>
    internal static bool Is(ITypeSymbol? type)
    {
        return type is not null
               && string.Equals(type.Name, "ByteSize", StringComparison.Ordinal);
    }
}
