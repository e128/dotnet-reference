namespace E128.Analyzers.FileSystem;

/// <summary>
///     What a path-like name points at.
/// </summary>
internal enum PathNameKind
{
    File = 0,
    Directory = 1,
    Ambiguous = 2
}
