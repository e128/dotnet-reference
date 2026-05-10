using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Architecture.Tests;

public sealed class CircularDependencyTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = ArchitectureBaseline.Instance;

    [Fact]
    [Trait("Category", "CI")]
    public void Services_ShouldNotDependOn_Repositories_ThatDependBack()
    {
        IArchRule rule = Types()
            .That().ResideInNamespace("E128.Reference.Core.Repositories")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("E128.Reference.Core.Services"));

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Models_ShouldNotDependOn_AnyOtherLayer()
    {
        IArchRule rule = Types()
            .That().ResideInNamespace("E128.Reference.Core.Models")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("E128.Reference.Core.Services")
                    .Or().ResideInNamespace("E128.Reference.Core.Repositories"));

        rule.Check(Architecture);
    }
}
