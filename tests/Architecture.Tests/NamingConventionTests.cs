using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Architecture.Tests;

/// <summary>
///     Enforces naming conventions across the Core assembly.
/// </summary>
public sealed class NamingConventionTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = ArchitectureBaseline.Instance;

    [Fact]
    [Trait("Category", "CI")]
    public void Interfaces_ShouldHave_IPrefix()
    {
        IArchRule rule = Interfaces()
            .Should().HaveNameStartingWith("I");

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Classes_ShouldNotUse_InterfaceNamingPattern()
    {
        // Catches "IFoo" (I + uppercase) but allows "InMemory...", "Internal...", etc.
        IArchRule rule = Classes()
            .That().AreNotAbstract()
            .Should().NotHaveNameMatching("^I[A-Z][a-z]");

        rule.Check(Architecture);
    }
}
