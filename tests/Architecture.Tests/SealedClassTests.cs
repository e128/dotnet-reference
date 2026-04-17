using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Architecture.Tests;

/// <summary>
///     Enforces the project convention that concrete classes should be sealed.
/// </summary>
public sealed class SealedClassTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = ArchitectureBaseline.Instance;

    [Fact]
    [Trait("Category", "CI")]
    public void ConcreteClasses_ShouldBe_Sealed()
    {
        IArchRule rule = Classes()
            .That().AreNotAbstract()
            .Should().BeSealed();

        rule.Check(Architecture);
    }
}
