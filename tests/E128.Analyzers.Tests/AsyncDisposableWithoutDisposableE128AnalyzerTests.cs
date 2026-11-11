using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncDisposableWithoutDisposableE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ClassWithIAsyncDisposableOnly_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncDisposableWithoutDisposableAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<AsyncDisposableWithoutDisposableAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<AsyncDisposableWithoutDisposableAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<AsyncDisposableWithoutDisposableAnalyzer>("""
                           using System;
                           using System.Threading.Tasks;

                           interface IC : IAsyncDisposable { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StructWithIAsyncDisposableOnly_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncDisposableWithoutDisposableAnalyzer>("""
                           using System;
                           using System.Threading.Tasks;

                           struct {|E128044:S|} : IAsyncDisposable
                           {
                               public ValueTask DisposeAsync() => ValueTask.CompletedTask;
                           }
                           """);
    }
}
