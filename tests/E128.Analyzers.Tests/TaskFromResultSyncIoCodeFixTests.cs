using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskFromResultSyncIoCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<TaskFromResultSyncIoAnalyzer, TaskFromResultSyncIoCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = 1,
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
}
