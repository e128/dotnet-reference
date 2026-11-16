using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DiskRoundtripCodeFixBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ConvertsBytesToTextWithUtf8GetString()
    {
        return CodeFixTestRunner.RunAsync<DiskRoundtripAnalyzer, DiskRoundtripCodeFixProvider>(
            """
            using System.IO;
            class C
            {
                string M(string path, byte[] bytes)
                {
                    File.WriteAllBytes(path, bytes);
                    return {|E128064:File.ReadAllText(path)|};
                }
            }
            """,
            """
            using System.IO;
            using System.Text;

            class C
            {
                string M(string path, byte[] bytes)
                {
                    File.WriteAllBytes(path, bytes);
                    return Encoding.UTF8.GetString(bytes);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheReadWithTheLinesSource()
    {
        return CodeFixTestRunner.RunAsync<DiskRoundtripAnalyzer, DiskRoundtripCodeFixProvider>(
            """
            using System.IO;
            class C
            {
                string[] M(string path, string[] lines)
                {
                    File.WriteAllLines(path, lines);
                    return {|E128064:File.ReadAllLines(path)|};
                }
            }
            """,
            """
            using System.IO;
            class C
            {
                string[] M(string path, string[] lines)
                {
                    File.WriteAllLines(path, lines);
                    return lines;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheWriteKindCannotConvertToTheReadKind()
    {
        return CodeFixTestRunner.NoFixAsync<DiskRoundtripAnalyzer, DiskRoundtripCodeFixProvider>(
            """
            using System.IO;
            class C
            {
                string[] M(string path, string content)
                {
                    File.WriteAllText(path, content);
                    return {|E128064:File.ReadAllLines(path)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheWriteHasNoSourceValue()
    {
        return CodeFixTestRunner.NoFixAsync<DiskRoundtripAnalyzer, DiskRoundtripCodeFixProvider>(
            """
            using System.IO;
            class C
            {
                string M(string path)
                {
                    using (var stream = File.Create(path)) { }
                    return {|E128064:File.ReadAllText(path)|};
                }
            }
            """);
    }
}
