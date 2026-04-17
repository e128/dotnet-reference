using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Verifies that the no-op code fix provider for E128037 does not offer a fix.
///     The <see cref="UnboundedTaskWhenAllCodeFixProvider" /> intentionally provides no fix
///     because adding SemaphoreSlim throttling is too structural for auto-fix.
/// </summary>
public sealed class UnboundedTaskWhenAllE128CodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task NoCodeFix_Offered_ForUnboundedWhenAll()
    {
        return VerifyNoFixAsync("""
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

    private static Task VerifyNoFixAsync(string code)
    {
        return new CSharpCodeFixTest<UnboundedTaskWhenAllAnalyzer, UnboundedTaskWhenAllCodeFixProvider, DefaultVerifier>
        {
            TestCode = code,
            FixedCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 0
        }.RunAsync();
    }
}
