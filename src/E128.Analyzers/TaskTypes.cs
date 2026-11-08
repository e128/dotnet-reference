using System;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers;

/// <summary>
///     Type queries for the task types that carry an asynchronous result.
/// </summary>
internal static class TaskTypes
{
    /// <summary>
    ///     Returns true for <c lang="csharp">Task</c> and <c lang="csharp">ValueTask</c>, including
    ///     their generic forms.
    /// </summary>
    internal static bool IsTaskOrValueTask(INamedTypeSymbol type)
    {
        var definition = type.OriginalDefinition;
        return (string.Equals(definition.Name, "Task", StringComparison.Ordinal)
                || string.Equals(definition.Name, "ValueTask", StringComparison.Ordinal))
               && Namespaces.Equals(definition.ContainingNamespace, "System.Threading.Tasks");
    }
}
