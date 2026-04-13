using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
/// Registration test for the FinallyBlockCleanup code fix provider.
/// Full code fix output tests are deferred — see analyzer tests for diagnostic coverage.
/// </summary>
public sealed class E128057FinallyBlockCleanupCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void FinallyCleanup_CodeFixProvider_IsRegistered()
    {
        var provider = new Reliability.FinallyBlockCleanupCodeFixProvider();
        Assert.Contains("E128057", provider.FixableDiagnosticIds);
    }
}
