using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Architecture.Tests;

/// <summary>
///     Generated from the project references. A production project must not use a first-party
///     production project outside its transitive references.
/// </summary>
public sealed class ProjectBoundaryTests
{
    // Coverage instrumentation injects a tracker type with one full name into every instrumented
    // assembly. ArchUnitNET merges it across assemblies, so it is excluded from both sides.
    private const string CoverageTrackerNamespace = "Microsoft.CodeCoverage.Instrumentation.Static.Tracker";

    private static readonly ArchUnitNET.Domain.Architecture Architecture = ProjectReferenceBaseline.Instance;

    [Fact]
    [Trait("Category", "CI")]
    public void E128_Analyzers_ShouldNotDependOn_UnreferencedProjects()
    {
        IArchRule rule = Types()
            .That().ResideInAssembly(ProjectReferenceBaseline.Load("E128.Analyzers"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    ProjectReferenceBaseline.Load("E128.Analyzers.CodeFixers"),
                    ProjectReferenceBaseline.Load("E128.Analyzers.Union"),
                    ProjectReferenceBaseline.Load("E128.Reference.Cli"),
                    ProjectReferenceBaseline.Load("E128.Reference.Core"),
                    ProjectReferenceBaseline.Load("E128.Reference.Web"))
                    .And().DoNotResideInNamespace(CoverageTrackerNamespace));

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void E128_Analyzers_Union_ShouldNotDependOn_UnreferencedProjects()
    {
        IArchRule rule = Types()
            .That().ResideInAssembly(ProjectReferenceBaseline.Load("E128.Analyzers.Union"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    ProjectReferenceBaseline.Load("E128.Analyzers.CodeFixers"),
                    ProjectReferenceBaseline.Load("E128.Reference.Cli"),
                    ProjectReferenceBaseline.Load("E128.Reference.Core"),
                    ProjectReferenceBaseline.Load("E128.Reference.Web"))
                    .And().DoNotResideInNamespace(CoverageTrackerNamespace));

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void E128_Analyzers_CodeFixers_ShouldNotDependOn_UnreferencedProjects()
    {
        IArchRule rule = Types()
            .That().ResideInAssembly(ProjectReferenceBaseline.Load("E128.Analyzers.CodeFixers"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    ProjectReferenceBaseline.Load("E128.Analyzers.Union"),
                    ProjectReferenceBaseline.Load("E128.Reference.Cli"),
                    ProjectReferenceBaseline.Load("E128.Reference.Core"),
                    ProjectReferenceBaseline.Load("E128.Reference.Web"))
                    .And().DoNotResideInNamespace(CoverageTrackerNamespace));

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void E128_Reference_Cli_ShouldNotDependOn_UnreferencedProjects()
    {
        IArchRule rule = Types()
            .That().ResideInAssembly(ProjectReferenceBaseline.Load("E128.Reference.Cli"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    ProjectReferenceBaseline.Load("E128.Analyzers"),
                    ProjectReferenceBaseline.Load("E128.Analyzers.CodeFixers"),
                    ProjectReferenceBaseline.Load("E128.Analyzers.Union"),
                    ProjectReferenceBaseline.Load("E128.Reference.Web"))
                    .And().DoNotResideInNamespace(CoverageTrackerNamespace));

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void E128_Reference_Core_ShouldNotDependOn_UnreferencedProjects()
    {
        IArchRule rule = Types()
            .That().ResideInAssembly(ProjectReferenceBaseline.Load("E128.Reference.Core"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    ProjectReferenceBaseline.Load("E128.Analyzers"),
                    ProjectReferenceBaseline.Load("E128.Analyzers.CodeFixers"),
                    ProjectReferenceBaseline.Load("E128.Analyzers.Union"),
                    ProjectReferenceBaseline.Load("E128.Reference.Cli"),
                    ProjectReferenceBaseline.Load("E128.Reference.Web"))
                    .And().DoNotResideInNamespace(CoverageTrackerNamespace));

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void E128_Reference_Web_ShouldNotDependOn_UnreferencedProjects()
    {
        IArchRule rule = Types()
            .That().ResideInAssembly(ProjectReferenceBaseline.Load("E128.Reference.Web"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    ProjectReferenceBaseline.Load("E128.Analyzers"),
                    ProjectReferenceBaseline.Load("E128.Analyzers.CodeFixers"),
                    ProjectReferenceBaseline.Load("E128.Analyzers.Union"),
                    ProjectReferenceBaseline.Load("E128.Reference.Cli"))
                    .And().DoNotResideInNamespace(CoverageTrackerNamespace));

        rule.Check(Architecture);
    }
}
