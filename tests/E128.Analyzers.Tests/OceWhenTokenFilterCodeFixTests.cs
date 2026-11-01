using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OceWhenTokenFilterCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<OceWhenTokenFilterAnalyzer, OceWhenTokenFilterCodeFixProvider>(source, fixedCode);

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
