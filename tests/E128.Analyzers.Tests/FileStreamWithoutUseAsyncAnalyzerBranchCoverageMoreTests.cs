using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileStreamWithoutUseAsyncAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyWithReferencesAsync(ReferenceAssemblies references, string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamWithoutUseAsyncAnalyzer>(
            code,
            options: new AnalyzerTestOptions
            {
                References = references,
                CompilerDiagnostics = CompilerDiagnostics.None
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompilationHasNoFileStream()
    {
        return VerifyWithReferencesAsync(
            ReferenceAssemblies.NetStandard.NetStandard10,
            """
            class C
            {
                void M()
                {
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompilationHasNoFileOptions()
    {
        return VerifyWithReferencesAsync(
            ReferenceAssemblies.NetStandard.NetStandard10,
            """
            namespace System.IO
            {
                public class FileStream
                {
                    public FileStream(string path, bool useAsync)
                    {
                    }
                }
            }

            class C
            {
                void M(string path)
                {
                    _ = new System.IO.FileStream(path, false);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenFileOptionsHasNoAsynchronousMember()
    {
        return VerifyWithReferencesAsync(
            ReferenceAssemblies.Net.Net100,
            """
            namespace System.IO
            {
                public enum FileOptions
                {
                    None = 0
                }
            }

            class C
            {
                void M(string path)
                {
                    _ = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read, 4096, false);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAsynchronousMemberIsZero()
    {
        return VerifyWithReferencesAsync(
            ReferenceAssemblies.Net.Net100,
            """
            namespace System.IO
            {
                public enum FileOptions
                {
                    None = 0,
                    Asynchronous = 0
                }
            }

            class C
            {
                void M(string path)
                {
                    _ = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read, 4096, false);
                }
            }
            """);
    }
}
