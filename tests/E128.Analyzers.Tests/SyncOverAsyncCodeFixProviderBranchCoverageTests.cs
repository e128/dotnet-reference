using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SyncOverAsyncCodeFixProviderBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<SyncOverAsyncAnalyzer, SyncOverAsyncCodeFixProvider>(source, fixedCode);
    }

    private static Task VerifyNoFixAsync(string code)
    {
        return CodeFixTestRunner.NoFixAsync<SyncOverAsyncAnalyzer, SyncOverAsyncCodeFixProvider>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheReturnTypeOfAnAlreadyAsyncMethod()
    {
        return VerifyFixAsync(
            """
            using System.Threading.Tasks;
            class C
            {
                async Task<int> M(Task<int> task)
                {
                    await Task.Yield();
                    return {|E128008:task.Result|};
                }
            }
            """,
            """
            using System.Threading.Tasks;
            class C
            {
                async Task<int> M(Task<int> task)
                {
                    await Task.Yield();
                    return await task;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_ForALocalFunction()
    {
        return VerifyNoFixAsync("""
            using System.Threading.Tasks;
            class C
            {
                int M(Task<int> task)
                {
                    int Local() => {|E128008:task.Result|};
                    return Local();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_ForAPropertyGetter()
    {
        return VerifyNoFixAsync("""
            using System.Threading.Tasks;
            class C
            {
                private Task<int> _task = Task.FromResult(1);
                int Value
                {
                    get { return {|E128008:_task.Result|}; }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_ForAnIndexer()
    {
        return VerifyNoFixAsync("""
            using System.Threading.Tasks;
            class C
            {
                private Task<int> _task = Task.FromResult(1);
                int this[int i] => {|E128008:_task.Result|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_ForAFieldInitializer()
    {
        return VerifyNoFixAsync("""
            using System.Threading.Tasks;
            class C
            {
                private int _value = {|E128008:Task.FromResult(1).Result|};
            }
            """);
    }

}
