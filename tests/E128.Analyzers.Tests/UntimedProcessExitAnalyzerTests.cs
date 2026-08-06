using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UntimedProcessExitAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<UntimedProcessExitAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NoArgWaitForExit()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(Process process)
                               {
                                   {|E128099:process.WaitForExit()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_TimeboxedWaitForExit()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           class C
                           {
                               void M(Process process)
                               {
                                   process.WaitForExit(5000);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NoArgAsyncWaitForExit()
    {
        return VerifyAsync("""
                           using System.Diagnostics;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M(Process process)
                               {
                                   await {|E128099:process.WaitForExitAsync()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ParameterCancellationToken()
    {
        return VerifyAsync("""
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

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_TimeoutCtsToken()
    {
        return VerifyAsync("""
                           using System;
                           using System.Diagnostics;
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M(Process process)
                               {
                                   using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                   await process.WaitForExitAsync(cts.Token);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_TimeSpanOverload()
    {
        return VerifyAsync("""
                           using System;
                           using System.Diagnostics;
                           class C
                           {
                               void M(Process process)
                               {
                                   process.WaitForExit(TimeSpan.FromSeconds(30));
                               }
                           }
                           """);
    }
}
