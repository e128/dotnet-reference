using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SyncLocalFunctionCallBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForOtherMembersAndAWaitMethodGroup()
    {
        return AnalyzerTestRunner.VerifyAsync<SyncLocalFunctionCallAnalyzer>("""
            using System;
            using System.Threading.Tasks;

            class C
            {
                void M()
                {
                    async Task LoadAsync() { await Task.CompletedTask; }
                    var awaiter = LoadAsync().GetAwaiter();
                    Action wait = LoadAsync().Wait;
                    _ = (awaiter, wait);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_Declines_WhenTheCallSitsInAConstructor()
    {
        return CodeFixTestRunner.NoFixAsync<SyncLocalFunctionCallAnalyzer, SyncLocalFunctionCallCodeFixProvider>(
            """
            using System.Threading.Tasks;

            class C
            {
                public C()
                {
                    async Task LoadAsync() { await Task.CompletedTask; }
                    {|E128096:LoadAsync()|}.Wait();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_Declines_WhenTheCallSitsInALambda()
    {
        return CodeFixTestRunner.NoFixAsync<SyncLocalFunctionCallAnalyzer, SyncLocalFunctionCallCodeFixProvider>(
            """
            using System;
            using System.Threading.Tasks;

            class C
            {
                void M()
                {
                    async Task<int> LoadAsync() => await Task.FromResult(1);
                    Func<int> read = () => {|E128096:LoadAsync()|}.Result;
                    _ = read();
                }
            }
            """);
    }
}
