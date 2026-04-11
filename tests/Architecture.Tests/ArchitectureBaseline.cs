using ArchUnitNET.Loader;

namespace Architecture.Tests;

/// <summary>
/// Shared architecture model — loaded once per test run via Mono.Cecil IL analysis.
/// </summary>
internal static class ArchitectureBaseline
{
    public static readonly ArchUnitNET.Domain.Architecture Instance =
        new ArchLoader()
            .LoadAssemblies(typeof(E128.Reference.Core.Greeter).Assembly)
            .Build();
}
