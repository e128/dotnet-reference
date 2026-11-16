using System.Threading.Tasks;
using E128.Analyzers.FileSystem;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileSystemPathAnalyzerBranchCoverageGapTests
{
    private static Task VerifyAsync(string code, CompilerDiagnostics? compilerDiagnostics = null)
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemPathAnalyzer>(
            code,
            options: new AnalyzerTestOptions
            {
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                CompilerDiagnostics = compilerDiagnostics
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAPathNamedParameterOfAnExpressionBodiedConstructor()
    {
        return VerifyAsync("""
            class C
            {
                string _stored = "";

                C(string {|E128001:path|}) => _stored = path;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForARecordWithoutAPrimaryConstructor()
    {
        return VerifyAsync("""
            record R
            {
                public string Path { get; init; } = "";
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenLocalsDoNotDeriveFromTheParameter()
    {
        return VerifyAsync("""
            using System.IO;

            class C
            {
                static string Compute(string text) => text;

                void M(string root)
                {
                    string declaredOnly;
                    var viaCall = Compute(root);
                    var viaEmptyPath = Path.Combine();
                    var viaLiteral = Path.Combine("a", root);
                    File.ReadAllText(viaCall);
                    File.ReadAllText(viaEmptyPath);
                    File.ReadAllText(viaLiteral);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnlyTheParameterThatReachesFileIo()
    {
        return VerifyAsync("""
            using System.IO;

            class C
            {
                string _external = "";

                void M(string {|E128001:source|}, string unused)
                {
                    var copy = source;
                    File.ReadAllText(copy);
                    File.Exists(_external);
                    File.Exists(unused.Trim());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAFileCallPassesTooFewArguments()
    {
        return VerifyAsync("""
            using System.IO;

            class C
            {
                void M(string root)
                {
                    File.Exists();
                    _ = root;
                }
            }
            """, CompilerDiagnostics.None);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFileCallCannotBindToASystemIoMethod()
    {
        return VerifyAsync("""
            using System.IO;

            class C
            {
                void M(string root)
                {
                    File.Exists(root, root);
                }
            }
            """, CompilerDiagnostics.None);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheConstructorCannotBindToASystemIoOverload()
    {
        return VerifyAsync("""
            using System.IO;

            class C
            {
                void M(string root)
                {
                    var info = new FileInfo(root, root);
                    _ = info;
                }
            }
            """, CompilerDiagnostics.None);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAKnownTypeIsCreatedWithoutAnArgumentList()
    {
        return VerifyAsync("""
            class FileInfo
            {
                public string Name { get; set; } = "";
            }

            class C
            {
                void M(string root)
                {
                    var info = new FileInfo { Name = root };
                    _ = info;
                }
            }
            """);
    }
}
