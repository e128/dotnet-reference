using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcurrencyLimitE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<ConcurrencyLimitAnalyzer, ConcurrencyLimitCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SemaphoreSlim_Zero_ReplacesWithProcessorCount()
    {
        const string source = """
            using System;
            using System.Threading;
            class C
            {
                void M()
                {
                    var s = {|E128040:new SemaphoreSlim(0)|};
                }
            }
            """;

        const string fixedCode = """
            using System;
            using System.Threading;
            class C
            {
                void M()
                {
                    var s = new SemaphoreSlim(Environment.ProcessorCount);
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }
}
