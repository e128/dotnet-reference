using System.Threading.Tasks;
using E128.Analyzers.FileSystem;
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
        return CodeFixTestRunner.RunAsync<FileSystemPathAnalyzer, FileSystemPathCodeFixProvider>(
            source,
            fixedCode,
            markupOptions: MarkupOptions.UseFirstDescriptor,
            options: new CodeFixTestOptions
            {
                FixAllIterations = 1,
                CodeActionEquivalenceKey = equivalenceKey,
                CodeActionIndex = codeActionIndex
            });
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
            "FileSystemPathCodeFixProvider_FileInfo");
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
            "FileSystemPathCodeFixProvider_DirectoryInfo");
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
            "FileSystemPathCodeFixProvider_FileInfo");
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
            "FileSystemPathCodeFixProvider_FileInfo");
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
            "FileSystemPathCodeFixProvider_FileInfo");
    }
}
