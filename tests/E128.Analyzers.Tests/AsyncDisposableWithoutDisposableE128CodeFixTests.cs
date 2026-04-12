using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncDisposableWithoutDisposableE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<AsyncDisposableWithoutDisposableAnalyzer, AsyncDisposableWithoutDisposableCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ClassWithIAsyncDisposableOnly_AddsIDisposable()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;

            class {|E128044:C|} : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }
            """;

        // The code fix inserts a Dispose() stub. The body braces are indented by
        // the Roslyn formatter but the method signature is not (no leading trivia).
        const string fixedCode = """
            using System;
            using System.Threading.Tasks;

            class C : IAsyncDisposable, IDisposable
            {
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public void Dispose()
                {
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }
}
