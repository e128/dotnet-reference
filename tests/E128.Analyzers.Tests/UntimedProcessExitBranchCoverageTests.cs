using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UntimedProcessExitBranchCoverageTests
{
    private const string Usings = "using System;\nusing System.Diagnostics;\nusing System.Threading;\nusing System.Threading.Tasks;\n";

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTokenIsCancellationTokenNone()
    {
        return AnalyzerTestRunner.VerifyAsync<UntimedProcessExitAnalyzer>(Usings + """
            class C
            {
                async Task M(Process process)
                {
                    await {|E128099:process.WaitForExitAsync(CancellationToken.None)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTokenComesFromAnotherSource()
    {
        return AnalyzerTestRunner.VerifyAsync<UntimedProcessExitAnalyzer>(Usings + """
            class C
            {
                private readonly CancellationTokenSource _source = new(TimeSpan.FromSeconds(5));
                private CancellationToken Token => _source.Token;

                async Task M(Process process, CancellationTokenSource parameterSource)
                {
                    var local = _source.Token;
                    await process.WaitForExitAsync(local);
                    await process.WaitForExitAsync(Token);
                    await process.WaitForExitAsync(_source.Token);
                    await process.WaitForExitAsync(parameterSource.Token);
                    await process.WaitForExitAsync(default);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheWaitIsOnAnotherType()
    {
        return AnalyzerTestRunner.VerifyAsync<UntimedProcessExitAnalyzer>(Usings + """
            class Waiter
            {
                public void WaitForExit() { }
                public Task WaitForExitAsync(CancellationToken token) => Task.CompletedTask;
            }
            class C
            {
                async Task M(Waiter waiter)
                {
                    waiter.WaitForExit();
                    await waiter.WaitForExitAsync(CancellationToken.None);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnUnqualifiedOrDifferentlyNamedCall()
    {
        return AnalyzerTestRunner.VerifyAsync<UntimedProcessExitAnalyzer>(Usings + """
            class C
            {
                void M(Process process)
                {
                    process.Start();
                    Local();

                    static void Local() { }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AddsTheTimeoutWhenTheTokenIsCancellationTokenNone()
    {
        return CodeFixTestRunner.RunAsync<UntimedProcessExitAnalyzer, UntimedProcessExitCodeFixProvider>(
            Usings + """
            class C
            {
                async Task M(Process process)
                {
                    await {|E128099:process.WaitForExitAsync(CancellationToken.None)|};
                }
            }
            """,
            Usings + """
            class C
            {
                async Task M(Process process)
                {
                    {
                        using var __timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        try
                        {
                            await process.WaitForExitAsync(__timeoutCts.Token);
                        }
                        catch (OperationCanceledException) when (__timeoutCts.IsCancellationRequested)
                        {
                            process.Kill(true);
                        }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheSyncWaitIsNotAStatement()
    {
        return CodeFixTestRunner.NoFixAsync<UntimedProcessExitAnalyzer, UntimedProcessExitCodeFixProvider>(
            Usings + """
            class C
            {
                void M(Process process) => {|E128099:process.WaitForExit()|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheAsyncWaitIsNotAwaitedInAStatement()
    {
        return CodeFixTestRunner.NoFixAsync<UntimedProcessExitAnalyzer, UntimedProcessExitCodeFixProvider>(
            Usings + """
            class C
            {
                Task M(Process process)
                {
                    var pending = {|E128099:process.WaitForExitAsync()|};
                    return pending;
                }
            }
            """);
    }
}
