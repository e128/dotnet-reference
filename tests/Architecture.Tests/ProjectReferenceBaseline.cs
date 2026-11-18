using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using ArchUnitNET.Loader;

namespace Architecture.Tests;

/// <summary>
///     Loads the first-party production assemblies once per test run.
/// </summary>
internal static class ProjectReferenceBaseline
{
    private static readonly ImmutableArray<string> AssemblyNames =
    [
        "E128.Analyzers",
        "E128.Analyzers.CodeFixers",
        "E128.Analyzers.Union",
        "E128.Reference.Cli",
        "E128.Reference.Core",
        "E128.Reference.Web"
    ];

    public static readonly ArchUnitNET.Domain.Architecture Instance =
        new ArchLoader().LoadAssemblies([.. AssemblyNames.Select(Load)]).Build();

    public static Assembly Load(string name)
    {
        return Assembly.Load(new AssemblyName(name));
    }
}
