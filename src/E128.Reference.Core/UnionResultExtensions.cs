using System;
using System.Runtime.CompilerServices;

namespace E128.Reference.Core;

/// <summary>
///     Composes a result union. The constraint admits every union and no other type, so the
///     compiler rejects the wrong call.
/// </summary>
public static class UnionResultExtensions
{
    /// <summary>
    ///     Projects a union value through a case switch.
    /// </summary>
    /// <typeparam name="TUnion">The union type. Only a union satisfies the constraint.</typeparam>
    /// <typeparam name="TOut">The projected type.</typeparam>
    /// <param name="value">The union value.</param>
    /// <param name="project">The switch over the cases.</param>
    /// <returns>The projected value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="project" /> is null.</exception>
    public static TOut Match<TUnion, TOut>(this TUnion value, Func<TUnion, TOut> project)
        where TUnion : IUnion
    {
        ArgumentNullException.ThrowIfNull(project);

        return project(value);
    }
}
