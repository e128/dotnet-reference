using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using E128.Reference.Core.Repositories;
using E128.Reference.Core.Services;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Architecture.Tests;

/// <summary>
///     Enforces service layer patterns: service classes should implement an interface.
/// </summary>
public sealed class ServicePatternTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = ArchitectureBaseline.Instance;

    [Fact]
    [Trait("Category", "CI")]
    public void ServiceClasses_ShouldImplement_IGreetingService()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespace("E128.Reference.Core.Services")
            .Should().ImplementInterface(typeof(IGreetingService));

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void ServiceClasses_ShouldHave_ServiceSuffix()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespace("E128.Reference.Core.Services")
            .Should().HaveNameEndingWith("Service");

        rule.Check(Architecture);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void RepositoryClasses_ShouldImplement_IGreetingRepository()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespace("E128.Reference.Core.Repositories")
            .Should().ImplementInterface(typeof(IGreetingRepository));

        rule.Check(Architecture);
    }
}
