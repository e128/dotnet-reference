using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OceWhenTokenFilterCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<OceWhenTokenFilterAnalyzer, OceWhenTokenFilterCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NegatedTokenFilter_FixedByRemovingFilter()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Threading;
            class C
            {
                void M(CancellationToken token)
                {
                    try
                    {
                    }
                    {|E128100:catch (OperationCanceledException) when (!token.IsCancellationRequested)|}
                    {
                        throw;
                    }
                }
            }
            """,
            """
            using System;
            using System.Threading;
            class C
            {
                void M(CancellationToken token)
                {
                    try
                    {
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                }
            }
            """);
    }
}
