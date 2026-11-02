using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskFromResultSyncIoCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return VerifyFixWithReferencesAsync(ReferenceAssemblies.Net.Net100, source, fixedCode);
    }

    private static Task VerifyFixWithReferencesAsync(
        ReferenceAssemblies referenceAssemblies,
        string source,
        string fixedCode)
    {
        return new CSharpCodeFixTest<TaskFromResultSyncIoAnalyzer, TaskFromResultSyncIoCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = referenceAssemblies,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ConvertsToAsync_WhenFileReadAllText()
    {
        return VerifyFixAsync(
            """
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
            """,
            """
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
    public Task CodeFix_ConvertsToAsync_WhenFileWriteAllBytes()
    {
        return VerifyFixAsync(
            """
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
            """,
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                async Task<bool> M(string path, byte[] data)
                {
                    await File.WriteAllBytesAsync(path, data);
                    return true;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_LeavesFileCallAlone_WhenTheAsyncFileApiIsMissing()
    {
        // Stream.ReadAsync exists on netstandard2.0 and File.ReadAllTextAsync does not. The fix
        // must convert the first call and leave the second, or the document does not compile.
        return VerifyFixWithReferencesAsync(
            ReferenceAssemblies.NetStandard.NetStandard20,
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                Task<int> M(Stream stream, byte[] buffer, string path)
                {
                    var count = stream.Read(buffer, 0, 1);
                    var text = File.ReadAllText(path);
                    return {|E128028:Task.FromResult(count)|};
                }
            }
            """,
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                async Task<int> M(Stream stream, byte[] buffer, string path)
                {
                    var count = await stream.ReadAsync(buffer, 0, 1);
                    var text = File.ReadAllText(path);
                    return count;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_LeavesSyncCallInsideLambdaAlone()
    {
        // A lambda cannot take an await unless it becomes async. Rewriting the call in place
        // would produce CS4033.
        return VerifyFixAsync(
            """
            using System;
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                Task<string> M(string path)
                {
                    Run(() => File.ReadAllText(path));
                    var text = File.ReadAllText(path);
                    return {|E128028:Task.FromResult(text)|};
                }

                static void Run(Action action) { }
            }
            """,
            """
            using System;
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                async Task<string> M(string path)
                {
                    Run(() => File.ReadAllText(path));
                    var text = await File.ReadAllTextAsync(path);
                    return text;
                }

                static void Run(Action action) { }
            }
            """);
    }
}
