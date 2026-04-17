using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskFromResultSyncIoAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TaskFromResultSyncIoAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskFromResultSyncIoAnalyzer_ReportsDiagnostic_WhenSyncFileReadAllTextWrappedInFromResult()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task<string> M(string path)
                               {
                                   var text = File.ReadAllText(path);
                                   return {|E128028:Task.FromResult(text)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskFromResultSyncIoAnalyzer_ReportsDiagnostic_WhenSyncFileWriteAllBytesWrappedInFromResult()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task<bool> M(string path, byte[] data)
                               {
                                   File.WriteAllBytes(path, data);
                                   return {|E128028:Task.FromResult(true)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskFromResultSyncIoAnalyzer_ReportsDiagnostic_WhenSyncFileReadAllLinesWrappedInFromResult()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task<string[]> M(string path)
                               {
                                   var lines = File.ReadAllLines(path);
                                   return {|E128028:Task.FromResult(lines)|};
                               }
                           }
                           """);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskFromResultSyncIoAnalyzer_NoDiagnostic_WhenPureExpressionWrapped()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               Task<int> M(int x)
                               {
                                   return Task.FromResult(x + 1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskFromResultSyncIoAnalyzer_NoDiagnostic_WhenMethodAlreadyAsync()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task<string> M(string path)
                               {
                                   var text = await File.ReadAllTextAsync(path);
                                   return text;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskFromResultSyncIoAnalyzer_NoDiagnostic_WhenConstantReturnedFromResult()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               Task<string> M()
                               {
                                   return Task.FromResult("hello");
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskFromResultSyncIoAnalyzer_NoDiagnostic_WhenNoSyncIoCallsExist()
    {
        return VerifyAsync("""
                           using System.Linq;
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task<IReadOnlyList<int>> M(IReadOnlyList<int> items, int topK)
                               {
                                   var result = items.Count <= topK
                                       ? items
                                       : items.Take(topK).ToList();
                                   return Task.FromResult(result);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskFromResultSyncIoAnalyzer_NoDiagnostic_WhenValueTaskFromResultWithNoIo()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               ValueTask<int> M()
                               {
                                   return ValueTask.FromResult(42);
                               }
                           }
                           """);
    }

    #endregion Does Not Fire
}
