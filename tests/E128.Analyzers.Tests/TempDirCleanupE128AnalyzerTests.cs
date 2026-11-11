using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TempDirCleanupE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task TempDirCleanup_Reports_WhenFieldInitializerHasGetTempPath()
    {
        return AnalyzerTestRunner.VerifyAsync<TempDirCleanupAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<TempDirCleanupAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<TempDirCleanupAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<TempDirCleanupAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<TempDirCleanupAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<TempDirCleanupAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<TempDirCleanupAnalyzer>("""
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
