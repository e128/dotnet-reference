using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileInfoToctouCodeFixBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_WrapsAReturnStatementAndReturnsDefaultFromTheCatch()
    {
        return CodeFixTestRunner.RunAsync<FileInfoToctouAnalyzer, FileInfoToctouCodeFixProvider>(
            """
            using System.IO;
            class Loader
            {
                string? Load(string path)
                {
                    if (!File.Exists(path)) return string.Empty;
                    return {|E128056:File.ReadAllText(path)|};
                }
            }
            """,
            """
            using System.IO;
            class Loader
            {
                string? Load(string path)
                {
                    if (!File.Exists(path)) return string.Empty;
                    try
                    {
                        return File.ReadAllText(path);
                    }
                    catch (System.IO.IOException)
                    {
                        return default;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_WrapsAStatementInAVoidMethodWithAnEmptyCatch()
    {
        return CodeFixTestRunner.RunAsync<FileInfoToctouAnalyzer, FileInfoToctouCodeFixProvider>(
            """
            using System.IO;
            class Loader
            {
                void Load(string path)
                {
                    if (!File.Exists(path)) return;
                    Consume({|E128056:File.ReadAllText(path)|});
                }

                static void Consume(string text) { }
            }
            """,
            """
            using System.IO;
            class Loader
            {
                void Load(string path)
                {
                    if (!File.Exists(path)) return;
                    try
                    {
                        Consume(File.ReadAllText(path));
                    }
                    catch (System.IO.IOException)
                    {
                    }
                }

                static void Consume(string text) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAValueReturningMethodReadsIntoALocal()
    {
        return CodeFixTestRunner.NoFixAsync<FileInfoToctouAnalyzer, FileInfoToctouCodeFixProvider>(
            """
            using System.IO;
            class Loader
            {
                string Load(string path)
                {
                    if (!File.Exists(path)) return string.Empty;
                    var text = {|E128056:File.ReadAllText(path)|};
                    return text;
                }
            }
            """);
    }
}
