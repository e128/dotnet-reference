namespace E128.Analyzers.Security;

/// <summary>
///     The result of classifying a type. The diagnostic text depends on the reporting rule, so the cache stores
///     only which rule owns the type.
/// </summary>
internal enum TypeVerdict
{
    Allowed = 0,
    ForbiddenName = 1,
    ForbiddenType = 2,
}
