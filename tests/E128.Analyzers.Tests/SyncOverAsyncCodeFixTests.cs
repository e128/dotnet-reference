using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SyncOverAsyncCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<SyncOverAsyncAnalyzer, SyncOverAsyncCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = 1,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesTaskResult_WithAwait()
    {
        const string source = """
            using System.Threading.Tasks;
            class C
            {
                int M()
                {
                    var task = Task.FromResult(42);
                    return {|E128008:task.Result|};
                }
            }
            """;

        const string fixedCode = """
            using System.Threading.Tasks;
            class C
            {
                async Task<int> M()
                {
                    var task = Task.FromResult(42);
                    return await task;
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesGetAwaiterGetResult_WithAwait()
    {
        const string source = """
            using System.Threading.Tasks;
            class C
            {
                int M()
                {
                    var task = Task.FromResult(42);
                    return task.{|E128008:GetAwaiter|}().GetResult();
                }
            }
            """;

        const string fixedCode = """
            using System.Threading.Tasks;
            class C
            {
                async Task<int> M()
                {
                    var task = Task.FromResult(42);
                    return await task;
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_PromotesVoidMethod_ToAsyncTask()
    {
        const string source = """
            using System.Threading.Tasks;
            class C
            {
                void M()
                {
                    var task = Task.FromResult(42);
                    _ = {|E128008:task.Result|};
                }
            }
            """;

        const string fixedCode = """
            using System.Threading.Tasks;
            class C
            {
                async Task M()
                {
                    var task = Task.FromResult(42);
                    _ = await task;
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }
}
