using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UntimedProcessExitAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public void Reports_NoArgWaitForExit()
    {
        Assert.Fail("AC-5: Process.WaitForExit() with no arguments must be flagged");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void NoReport_TimeboxedWaitForExit()
    {
        Assert.Fail("AC-6: Process.WaitForExit(int) with a timeout argument must not be flagged");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Reports_NoArgAsyncWaitForExit()
    {
        Assert.Fail("AC-7: Process.WaitForExitAsync() with no arguments must be flagged");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Reports_ParameterCancellationToken()
    {
        Assert.Fail("AC-8: WaitForExitAsync(ct) with a parameter or CancellationToken.None must be flagged");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void NoReport_TimeoutCtsToken()
    {
        Assert.Fail("AC-9: WaitForExitAsync(cts.Token) with an in-method CancellationTokenSource must not be flagged");
    }

    [Fact]
    [Trait("Category", "CI")]
    public void NoReport_TimeSpanOverload()
    {
        Assert.Fail("AC-10: WaitForExitAsync(TimeSpan, ct) must not be flagged");
    }
}
