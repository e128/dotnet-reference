using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncFileIoAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { MarkupOptions = MarkupOptions.UseFirstDescriptor };

    /// <summary>
    ///     AC-1: E128092 fires on a sync File.* call inside a fully synchronous method.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncFileIoAnalyzer_Fires_OnSyncFileCallInSynchronousMethod()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncFileIoAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               void M(string path)
                               {
                                   {|E128092:File.ReadAllText(path)|};
                               }
                           }
                           """, options: Options);
    }

    /// <summary>
    ///     AC-2: E128092 does not fire when the containing method is already async (CA1849 covers it).
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncFileIoAnalyzer_DoesNotFire_WhenContainingMethodAlreadyAsync()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncFileIoAnalyzer>("""
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
                           """, options: Options);
    }

    /// <summary>
    ///     AC-3: E128092 does not fire on a File.* member with no Async sibling (e.g. File.Exists).
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task AsyncFileIoAnalyzer_DoesNotFire_OnFileMemberWithoutAsyncSibling()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncFileIoAnalyzer>("""
                           using System.IO;

                           class C
                           {
                               bool M(string path)
                               {
                                   return File.Exists(path);
                               }
                           }
                           """, options: Options);
    }
}
