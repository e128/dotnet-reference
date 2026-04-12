using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnboundedTaskWhenAllE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<UnboundedTaskWhenAllAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_AsyncSelect_NoThrottle_FiresE128037()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading.Tasks;
            class C
            {
                async Task M()
                {
                    var items = new List<string> { "a", "b" };
                    await {|E128037:Task.WhenAll(items.Select(async x => await Task.Delay(1)))|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_AsyncSelect_WithSemaphore_DoesNotFire()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                private static readonly SemaphoreSlim _sem = new(5);
                async Task M()
                {
                    var items = new List<string> { "a", "b" };
                    await Task.WhenAll(items.Select(async x =>
                    {
                        await _sem.WaitAsync();
                        try { await Task.Delay(1); }
                        finally { _sem.Release(); }
                    }));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_SyncSelect_DoesNotFire()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading.Tasks;
            class C
            {
                async Task M()
                {
                    var items = new List<string> { "a", "b" };
                    await Task.WhenAll(items.Select(x => Task.Delay(1)));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_MultipleArgs_DoesNotFire()
    {
        return VerifyAsync("""
            using System.Threading.Tasks;
            class C
            {
                async Task M()
                {
                    await Task.WhenAll(Task.Delay(1), Task.Delay(2));
                }
            }
            """);
    }
}
