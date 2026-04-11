using System.Threading.Tasks;
using E128.Analyzers.FileSystem;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileSystemPathCodeFixTests
{
    private static Task VerifyFixAsync(
        string source,
        string fixedCode,
        string? equivalenceKey = null,
        int? codeActionIndex = null)
    {
        var test = new CSharpCodeFixTest<FileSystemPathAnalyzer, FileSystemPathCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
            NumberOfFixAllIterations = 1,
        };

        if (equivalenceKey is not null)
        {
            test.CodeActionEquivalenceKey = equivalenceKey;
        }

        if (codeActionIndex is not null)
        {
            test.CodeActionIndex = codeActionIndex.Value;
        }

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_EmptyBody_FixChangesToFileInfo()
    {
        const string source = """
            class C { void M(string {|E128001:path|}) { } }
            """;

        const string fixedCode = """
            using System.IO;
            class C { void M(FileInfo path) { } }
            """;

        return VerifyFixAsync(source, fixedCode,
            equivalenceKey: "FileSystemPathCodeFixProvider_FileInfo");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_EmptyBody_FixChangesToDirectoryInfo()
    {
        const string source = """
            class C { void M(string {|E128001:path|}) { } }
            """;

        const string fixedCode = """
            using System.IO;
            class C { void M(DirectoryInfo path) { } }
            """;

        return VerifyFixAsync(source, fixedCode,
            equivalenceKey: "FileSystemPathCodeFixProvider_DirectoryInfo");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_NoBody_Interface_FixChangesToFileInfo()
    {
        const string source = """
            interface I { void M(string {|E128001:path|}); }
            """;

        const string fixedCode = """
            using System.IO;
            interface I { void M(FileInfo path); }
            """;

        return VerifyFixAsync(source, fixedCode,
            equivalenceKey: "FileSystemPathCodeFixProvider_FileInfo");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_Constructor_FixChangesToFileInfo()
    {
        const string source = """
            class C { public C(string {|E128001:path|}) { } }
            """;

        const string fixedCode = """
            using System.IO;
            class C { public C(FileInfo path) { } }
            """;

        return VerifyFixAsync(source, fixedCode,
            equivalenceKey: "FileSystemPathCodeFixProvider_FileInfo");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExistingUsingSystemIO_DoesNotDuplicate()
    {
        const string source = """
            using System.IO;
            class C { void M(string {|E128001:filePath|}) { } }
            """;

        const string fixedCode = """
            using System.IO;
            class C { void M(FileInfo filePath) { } }
            """;

        return VerifyFixAsync(source, fixedCode,
            equivalenceKey: "FileSystemPathCodeFixProvider_FileInfo");
    }
}
