using Microsoft.CodeAnalysis;

namespace E128.Analyzers.Testing;

/// <summary>
///     One matched low-value test condition, with the location the condition was found at.
/// </summary>
internal sealed class ConditionHit
{
    internal ConditionHit(ConditionKind kind, Location location)
    {
        Kind = kind;
        Location = location;
    }

    internal ConditionKind Kind { get; }

    internal Location Location { get; }
}
