using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CatchFilterOceE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<CatchFilterOceAnalyzer, CatchFilterOceCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = 1,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsOce_ToSingleNotFilter()
    {
        const string source = """
            using System;
            class C
            {
                void M()
                {
                    try { }
                    {|E128039:catch (Exception ex) when (ex is not OutOfMemoryException)|}
                    {
                    }
                }
            }
            """;

        const string fixedCode = """
            using System;
            class C
            {
                void M()
                {
                    try { }
                    catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
                    {
                    }
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }
}
