using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Architecture.Tests;

/// <summary>
///     Checks that the project-boundary baseline still covers the frozen production inventory.
///     The expected lists are literals captured from the evaluated project graph. They stay separate
///     from the loader and the rules that they verify.
/// </summary>
public sealed class ProjectInventoryTests
{
    private static readonly ImmutableArray<string> ExpectedAssemblies =
    [
        "E128.Analyzers",
        "E128.Analyzers.CodeFixers",
        "E128.Analyzers.Union",
        "E128.Reference.Cli",
        "E128.Reference.Core",
        "E128.Reference.Web"
    ];

    private static readonly ImmutableArray<string> ExpectedBoundaryRules =
    [
        "E128_Analyzers_CodeFixers_ShouldNotDependOn_UnreferencedProjects",
        "E128_Analyzers_ShouldNotDependOn_UnreferencedProjects",
        "E128_Analyzers_Union_ShouldNotDependOn_UnreferencedProjects",
        "E128_Reference_Cli_ShouldNotDependOn_UnreferencedProjects",
        "E128_Reference_Core_ShouldNotDependOn_UnreferencedProjects",
        "E128_Reference_Web_ShouldNotDependOn_UnreferencedProjects"
    ];

    private static readonly ArchUnitNET.Domain.Architecture Architecture = ProjectReferenceBaseline.Instance;

    [Fact]
    [Trait("Category", "CI")]
    public void Baseline_LoadsExactlyTheExpectedAssemblies()
    {
        string[] loaded =
        [
            .. Architecture.Assemblies
                .Where(static assembly => !assembly.IsOnlyReferenced)
                .Select(static assembly => new AssemblyName(assembly.FullName).Name ?? string.Empty)
                .Order(StringComparer.Ordinal)
        ];

        Assert.Equal(ExpectedAssemblies, loaded, StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ExpectedAssemblies_LoadAndContainTypes()
    {
        foreach (var name in ExpectedAssemblies)
        {
            Assert.Equal(name, ProjectReferenceBaseline.Load(name).GetName().Name);
            Assert.Contains(
                Architecture.Types,
                type => string.Equals(new AssemblyName(type.Assembly.FullName).Name, name, StringComparison.Ordinal));
        }
    }

    [Fact]
    [Trait("Category", "CI")]
    public void BoundaryRules_CoverEveryExpectedSubject()
    {
        string[] rules =
        [
            .. typeof(ProjectBoundaryTests)
                .GetMethods()
                .Where(static method => Attribute.IsDefined(method, typeof(FactAttribute)))
                .Select(static method => method.Name)
                .Order(StringComparer.Ordinal)
        ];

        Assert.Equal(ExpectedBoundaryRules, rules, StringComparer.Ordinal);
    }
}
