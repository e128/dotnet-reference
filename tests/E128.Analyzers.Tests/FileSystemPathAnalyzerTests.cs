using System.Threading.Tasks;
using E128.Analyzers.FileSystem;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileSystemPathAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { MarkupOptions = MarkupOptions.UseFirstDescriptor };

    private static Task VerifyWithRecordsAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FileSystemPathAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_PathParam_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemPathAnalyzer>("""
                           class C { void M(string {|E128001:path|}) { } }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_FilePathParam_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemPathAnalyzer>("""
                           class C { void M(string {|E128001:filePath|}) { } }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_DirectoryParam_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemPathAnalyzer>("""
                           class C { void M(string {|E128001:directory|}) { } }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_ConstructorParam_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemPathAnalyzer>("""
                           class C { public C(string {|E128001:path|}) { } }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_RecordPrimaryConstructor_PathParam_Fires()
    {
        return VerifyWithRecordsAsync("""
                                      record R(string {|E128001:htmlFilePath|}, string title);
                                      """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_XPathParam_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemPathAnalyzer>("""
                           class C { void M(string xPath) { } }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_MessageParam_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemPathAnalyzer>("""
                           class C { void M(string message) { } }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_PathParam_NonEmptyBodyNoIO_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemPathAnalyzer>("""
                           class C
                           {
                               void M(string path)
                               {
                                   _ = path.Length;
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_PathParam_NoBody_StillFires()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemPathAnalyzer>("""
                           interface I { void M(string {|E128001:path|}); }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamePattern_Message_SuggestsAmbiguous()
    {
        const string code = """
                            class C { void M(string path) { } }
                            """;

        var expected = DiagnosticResult
            .CompilerWarning("E128001")
            .WithLocation(1, 25)
            .WithMessage("Parameter 'path' appears to represent a file system path. Consider using 'FileInfo' or 'DirectoryInfo' instead of 'string'.");

        return CSharpAnalyzerVerifier<FileSystemPathAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(code, expected);
    }
}
