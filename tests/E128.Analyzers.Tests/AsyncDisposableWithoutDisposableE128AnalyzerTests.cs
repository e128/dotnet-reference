using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncDisposableWithoutDisposableE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<AsyncDisposableWithoutDisposableAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ClassWithIAsyncDisposableOnly_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;

                           class {|E128044:C|} : IAsyncDisposable
                           {
                               public ValueTask DisposeAsync() => ValueTask.CompletedTask;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ClassWithBothInterfaces_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;

                           class C : IAsyncDisposable, IDisposable
                           {
                               public ValueTask DisposeAsync() => ValueTask.CompletedTask;
                               public void Dispose() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ClassWithOnlyIDisposable_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;

                           class C : IDisposable
                           {
                               public void Dispose() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InterfaceWithIAsyncDisposable_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;

                           interface IC : IAsyncDisposable { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StructWithIAsyncDisposableOnly_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;

                           struct {|E128044:S|} : IAsyncDisposable
                           {
                               public ValueTask DisposeAsync() => ValueTask.CompletedTask;
                           }
                           """);
    }
}
