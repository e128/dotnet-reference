using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileInfoToctouBranchCoverageTests
{
    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>(
            "using System;\nusing System.IO;\nclass Config { public string Input = \"\"; public string Output = \"\"; public FileInfo Info = new FileInfo(\"x\"); }\n"
            + "class Marker { public bool Exists => true; }\nclass C\n{\n" + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAGuardOnAMemberPathAndSilentForASiblingMember()
    {
        return VerifyAsync("""
                string M(Config config)
                {
                    if (!File.Exists(config.Input)) return string.Empty;
                    var other = File.ReadAllText(config.Output);
                    return {|E128056:File.ReadAllText(config.Input)|} + other;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAGuardThroughAFileInfoStoredInAMember()
    {
        return VerifyAsync("""
                string M(Config config)
                {
                    if (!config.Info.Exists) return string.Empty;
                    return {|E128056:File.ReadAllText(config.Info.FullName)|};
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardedValueIsTheResultOfACall()
    {
        return VerifyAsync("""
                static string Path() => "x";

                string M()
                {
                    if (!File.Exists(Path())) return string.Empty;
                    return File.ReadAllText(Path());
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheExistsPropertyBelongsToAnotherType()
    {
        return VerifyAsync("""
                string M(Marker marker, string path)
                {
                    if (!marker.Exists) return string.Empty;
                    return File.ReadAllText(path);
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReadSitsInsideATryCatch()
    {
        return VerifyAsync("""
                string M(string path)
                {
                    if (!File.Exists(path)) return string.Empty;
                    try
                    {
                        return File.ReadAllText(path);
                    }
                    catch (IOException)
                    {
                        return string.Empty;
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTryHasOnlyAFinallyBlock()
    {
        return VerifyAsync("""
                string M(string path)
                {
                    if (!File.Exists(path)) return string.Empty;
                    try
                    {
                        return {|E128056:File.ReadAllText(path)|};
                    }
                    finally
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAReadWithoutArgumentsOrOfAnotherFileType()
    {
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>("""
            using System.IO;
            class File2
            {
                public static bool Exists(string path) => true;
                public static string ReadAllText(string path) => path;
            }
            class C
            {
                string M(string path)
                {
                    if (!File2.Exists(path)) return string.Empty;
                    return File2.ReadAllText(path);
                }
            }
            """);
    }
}
