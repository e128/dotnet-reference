using System.Threading.Tasks;
using E128.Analyzers.FileSystem;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileSystemPathAnalyzerUseSiteTests
{
    private static Task VerifyAsync(string testCode, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FileSystemPathAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UseSite_FileReadAllText_FiresAsFileInfo()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void M(string {|E128001:p|})
                {
                    File.ReadAllText(p);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UseSite_DirectoryGetFiles_FiresAsDirectoryInfo()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void M(string {|E128001:p|})
                {
                    Directory.GetFiles(p);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UseSite_NewFileInfo_FiresAsFileInfo()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void M(string {|E128001:p|})
                {
                    _ = new FileInfo(p);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UseSite_NewStreamReader_FiresAsFileInfo()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void M(string {|E128001:p|})
                {
                    _ = new StreamReader(p);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UseSite_PathCombineThenDirectory_FiresOneHop()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                void M(string {|E128001:p|})
                {
                    var combined = Path.Combine(p, "sub");
                    Directory.GetFiles(combined);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UseSite_MessageParam_NoFire()
    {
        return VerifyAsync("""
            class C
            {
                void M(string message) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UseSite_FileUsage_MessageSuggestsFileInfo()
    {
        const string code = """
            using System.IO;
            class C
            {
                void M(string p)
                {
                    File.ReadAllText(p);
                }
            }
            """;

        var expected = DiagnosticResult
            .CompilerWarning("E128001")
            .WithLocation(4, 19)
            .WithMessage("Parameter 'p' appears to represent a file path. Consider using 'FileInfo' instead of 'string'.");

        return VerifyAsync(code, expected);
    }
}
