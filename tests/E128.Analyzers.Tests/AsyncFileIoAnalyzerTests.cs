using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncFileIoAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<AsyncFileIoAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>AC-1: E128092 fires on a sync File.* call inside a fully synchronous method.</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncFileIoAnalyzer_Fires_OnSyncFileCallInSynchronousMethod()
    {
        return VerifyAsync("""
                           using System.IO;

                           class C
                           {
                               void M(string path)
                               {
                                   {|E128092:File.ReadAllText(path)|};
                               }
                           }
                           """);
    }

    /// <summary>AC-2: E128092 does not fire when the containing method is already async (CA1849 covers it).</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncFileIoAnalyzer_DoesNotFire_WhenContainingMethodAlreadyAsync()
    {
        return VerifyAsync("""
                           using System.IO;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(string path)
                               {
                                   File.ReadAllText(path);
                                   await Task.CompletedTask;
                               }
                           }
                           """);
    }

    /// <summary>AC-3: E128092 does not fire on a File.* member with no Async sibling (e.g. File.Exists).</summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncFileIoAnalyzer_DoesNotFire_OnFileMemberWithoutAsyncSibling()
    {
        return VerifyAsync("""
                           using System.IO;

                           class C
                           {
                               bool M(string path)
                               {
                                   return File.Exists(path);
                               }
                           }
                           """);
    }
}
