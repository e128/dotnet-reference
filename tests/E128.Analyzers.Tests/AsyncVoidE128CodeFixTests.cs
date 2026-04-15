using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncVoidE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<AsyncVoidAnalyzer, AsyncVoidCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncVoid_CodeFix_ChangesVoidToTask()
    {
        const string source = """
            using System.Threading.Tasks;
            class C
            {
                async void {|E128007:DoWork|}()
                {
                    await Task.Delay(1);
                }
            }
            """;

        const string fixedCode = """
            using System.Threading.Tasks;
            class C
            {
                async Task DoWork()
                {
                    await Task.Delay(1);
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncVoid_CodeFix_AddsUsingWhenMissing()
    {
        const string source = """
            class C
            {
                async void {|E128007:DoWork|}()
                {
                }
            }
            """;

        const string fixedCode = """
            using System.Threading.Tasks;
            class C
            {
                async Task DoWork()
                {
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }
}
