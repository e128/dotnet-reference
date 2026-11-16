using System.Threading.Tasks;
using E128.Analyzers.FileSystem;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileSystemPathCodeFixBranchCoverageTests
{
    private const string OptionStub =
        "class Option<T> { public Option(string name) { } }\n" +
        "class Argument<T> { public Argument(string name) { } }\n";

    private static Task RunOptionAsync(string source, string typeName)
    {
        var fixedCode = "using System.IO;\n\n"
                        + OptionStub
                        + source.Replace("{|E128001:string|}", typeName, System.StringComparison.Ordinal);
        return CodeFixTestRunner.RunAsync<FileSystemPathAnalyzer, FileSystemPathCodeFixProvider>(
            OptionStub + source,
            fixedCode,
            markupOptions: MarkupOptions.UseFirstDescriptor);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ChangesAFileOptionToFileInfo()
    {
        return RunOptionAsync(
            """class C { void M() { var opt = new Option<{|E128001:string|}>("--file"); } }""",
            "FileInfo");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ChangesADirectoryArgumentToDirectoryInfo()
    {
        return RunOptionAsync(
            """class C { void M() { var arg = new Argument<{|E128001:string|}>("directory"); } }""",
            "DirectoryInfo");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_OffersNothingForAParameterUsedByFileIo()
    {
        const string code = """
            using System.IO;

            class C
            {
                string M(string {|E128001:target|})
                {
                    return File.ReadAllText(target);
                }
            }
            """;

        return CodeFixTestRunner.RunAsync<FileSystemPathAnalyzer, FileSystemPathCodeFixProvider>(
            code,
            code.Replace("{|E128001:target|}", "target", System.StringComparison.Ordinal),
            markupOptions: MarkupOptions.UseFirstDescriptor,
            options: new CodeFixTestOptions
            {
                FixedExpected = [new DiagnosticResult("E128001", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithSpan(5, 21, 5, 27)
                    .WithArguments("target", "file path", "FileInfo")]
            });
    }
}
