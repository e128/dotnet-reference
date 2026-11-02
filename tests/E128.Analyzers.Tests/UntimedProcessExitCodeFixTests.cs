using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UntimedProcessExitCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<UntimedProcessExitAnalyzer, UntimedProcessExitCodeFixProvider>(source, fixedCode);

    private static Task VerifyNoFixAsync(string source, params DiagnosticResult[] expected) =>
        CodeFixTestRunner.NoFixAsync<UntimedProcessExitAnalyzer, UntimedProcessExitCodeFixProvider>(source, expected: expected);

    [Fact]
    [Trait("Category", "CI")]
    public Task NoArgWaitForExit_FixedToTimeoutAndKill()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Diagnostics;
            class C
            {
                void M(Process process)
                {
                    {|E128099:process.WaitForExit()|};
                }
            }
            """,
            """
            using System;
            using System.Diagnostics;
            class C
            {
                void M(Process process)
                {
                    if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
                    {
                        process.Kill();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoArgWaitForExitAsync_FixedToTimeoutCtsAndKill()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Diagnostics;
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                async Task M(Process process)
                {
                    await {|E128099:process.WaitForExitAsync()|};
                }
            }
            """,
            """
            using System;
            using System.Diagnostics;
            using System.Threading;
            using System.Threading.Tasks;
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
    public Task ParameterCancellationToken_NoFixOffered()
    {
        return VerifyNoFixAsync("""
                                 using System.Diagnostics;
                                 using System.Threading;
                                 using System.Threading.Tasks;
                                 class C
                                 {
                                     async Task M(Process process, CancellationToken cancellationToken)
                                     {
                                         await {|E128099:process.WaitForExitAsync(cancellationToken)|};
                                     }
                                 }
                                 """);
    }
}
