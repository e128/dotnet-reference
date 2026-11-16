using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AwaitInFinallyBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnAwaitUsingDeclaration()
    {
        return AnalyzerTestRunner.VerifyAsync<AwaitInFinallyAnalyzer>("""
            using System;
            using System.Threading.Tasks;

            class C
            {
                static async Task M(IAsyncDisposable d)
                {
                    try
                    {
                    }
                    finally
                    {
                        {|E128186:await|} using var scope = d;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnAwaitInAnAnonymousMethodInsideFinally()
    {
        return AnalyzerTestRunner.VerifyAsync<AwaitInFinallyAnalyzer>("""
            using System;
            using System.Threading.Tasks;

            class C
            {
                static async Task M()
                {
                    try
                    {
                    }
                    finally
                    {
                        Func<Task> later = async delegate { await Task.Yield(); };
                        _ = later;
                    }

                    await Task.Yield();
                }
            }
            """);
    }
}
