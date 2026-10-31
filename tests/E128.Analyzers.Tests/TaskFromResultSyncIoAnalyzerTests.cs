using System;
using System.Linq;
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

    private static Task VerifyWithReferencesAsync(ReferenceAssemblies referenceAssemblies, string code)
    {
        var test = new CSharpAnalyzerTest<TaskFromResultSyncIoAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = referenceAssemblies
        };
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public void SyncIoTable_AndAsyncNameTable_ShareTheSameKeys()
    {
        // The code fix reads the async name for every call the analyzer reports. A key in one
        // table and not the other leaves the fix unable to convert the call.
        var reported = TaskFromResultSyncIoAnalyzer.SyncIoMethodsWithAsyncAlternatives.Keys
            .Order(StringComparer.Ordinal)
            .ToArray();
        var rewritable = TaskFromResultSyncIoAnalyzer.SyncToAsyncMethodNames.Keys
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(reported, rewritable);
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

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskFromResultSyncIoAnalyzer_ReportsDiagnostic_WhenTheOnlyAwaitIsInsideALambda()
    {
        // An await inside a lambda does not make the enclosing method async.
        return VerifyAsync("""
                           using System;
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task<string> M(string path)
                               {
                                   Run(async () => await Task.Yield());
                                   var text = File.ReadAllText(path);
                                   return {|E128028:Task.FromResult(text)|};
                               }

                               static void Run(Func<Task> action) { }
                           }
                           """);
    }

    #endregion Fires

    #region Does Not Fire

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskFromResultSyncIoAnalyzer_NoDiagnostic_WhenTheAsyncAlternativeIsMissing()
    {
        // File.ReadAllTextAsync arrived after netstandard2.0. Reporting there would give a fix
        // that does not compile, so the rule stays silent.
        return VerifyWithReferencesAsync(ReferenceAssemblies.NetStandard.NetStandard20, """
                                         using System.IO;
                                         using System.Threading.Tasks;
                                         class C
                                         {
                                             Task<string> M(string path)
                                             {
                                                 var text = File.ReadAllText(path);
                                                 return Task.FromResult(text);
                                             }
                                         }
                                         """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TaskFromResultSyncIoAnalyzer_NoDiagnostic_WhenTheCallHasNoAwaitableEquivalent()
    {
        // File.Copy and File.ReadLines have no overload that awaits. Reporting them would leave
        // a fix that adds async and no await, so the rule stays silent.
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task M(string source, string target)
                               {
                                   File.Copy(source, target);
                                   File.ReadLines(source);
                                   return Task.FromResult(0);
                               }
                           }
                           """);
    }

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
