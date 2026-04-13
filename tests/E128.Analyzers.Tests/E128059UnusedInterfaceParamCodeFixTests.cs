using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
/// Registration test for the UnusedInterfaceParam code fix provider.
/// Rename-based code fix tests require workspace support not available in the unit test harness.
/// </summary>
public sealed class E128059UnusedInterfaceParamCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void UnusedInterfaceParam_CodeFixProvider_IsRegistered()
    {
        var provider = new Design.UnusedInterfaceParamCodeFixProvider();
        Assert.Contains("E128059", provider.FixableDiagnosticIds);
    }
}
