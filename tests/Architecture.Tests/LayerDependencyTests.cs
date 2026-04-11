using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Architecture.Tests;

/// <summary>
/// Enforces layer dependency direction: Models → (nothing), Services → Models + Repositories, Repositories → Models.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = ArchitectureBaseline.Instance;

    [Fact]
    [Trait("Category", "CI")]
    public void Models_ShouldNotDependOn_Services()
    {
        IArchRule rule = Types()
            .That().ResideInNamespace("E128.Reference.Core.Models")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("E128.Reference.Core.Services"));

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Models_ShouldNotDependOn_Repositories()
    {
        IArchRule rule = Types()
            .That().ResideInNamespace("E128.Reference.Core.Models")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("E128.Reference.Core.Repositories"));

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Repositories_ShouldNotDependOn_Services()
    {
        IArchRule rule = Types()
            .That().ResideInNamespace("E128.Reference.Core.Repositories")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("E128.Reference.Core.Services"));

        rule.Check(Architecture);
    }
}
