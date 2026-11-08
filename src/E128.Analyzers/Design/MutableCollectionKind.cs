namespace E128.Analyzers.Design;

/// <summary>
///     The mutable collection whose read-only exposure the rules guard.
/// </summary>
internal enum MutableCollectionKind
{
    List = 0,
    Dictionary = 1
}
