using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TempDirCleanupE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TempDirCleanupAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TempDirCleanup_Reports_WhenFieldInitializerHasGetTempPath()
    {
        return VerifyAsync("""
                           using System.IO;
                           class {|E128054:MyTests|}
                           {
                               private readonly string _path = Path.Combine(Path.GetTempPath(), "test");
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TempDirCleanup_Reports_WhenConstructorHasGetTempPath()
    {
        return VerifyAsync("""
                           using System.IO;
                           class {|E128054:MyTests|}
                           {
                               private readonly string _path;
                               public MyTests()
                               {
                                   _path = Path.Combine(Path.GetTempPath(), "test");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TempDirCleanup_NoReport_WhenIAsyncLifetimeImplemented()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           interface IAsyncLifetime
                           {
                               Task InitializeAsync();
                               Task DisposeAsync();
                           }
                           class MyTests : IAsyncLifetime
                           {
                               private readonly string _path = Path.Combine(Path.GetTempPath(), "test");
                               public Task InitializeAsync() => Task.CompletedTask;
                               public Task DisposeAsync() => Task.CompletedTask;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TempDirCleanup_NoReport_WhenIDisposableImplemented()
    {
        return VerifyAsync("""
                           using System;
                           using System.IO;
                           class MyTests : IDisposable
                           {
                               private readonly string _path = Path.Combine(Path.GetTempPath(), "test");
                               public void Dispose() { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TempDirCleanup_NoReport_WhenIAsyncDisposableImplemented()
    {
        return VerifyAsync("""
                           using System;
                           using System.IO;
                           using System.Threading.Tasks;
                           class MyTests : IAsyncDisposable
                           {
                               private readonly string _path;
                               public MyTests()
                               {
                                   _path = Path.Combine(Path.GetTempPath(), "test");
                               }
                               public ValueTask DisposeAsync() => default;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TempDirCleanup_NoReport_WhenNoGetTempPath()
    {
        return VerifyAsync("""
                           using System.IO;
                           class MyTests
                           {
                               private readonly string _path = Path.Combine("/some/path", "test");
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TempDirCleanup_NoReport_WhenGetTempPathInMethod()
    {
        return VerifyAsync("""
                           using System.IO;
                           class MyTests
                           {
                               public void DoWork()
                               {
                                   var path = Path.Combine(Path.GetTempPath(), "test");
                               }
                           }
                           """);
    }
}
