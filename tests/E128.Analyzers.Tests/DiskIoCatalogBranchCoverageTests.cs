using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class DiskIoCatalogBranchCoverageTests
{
    [Theory]
    [Trait("Category", "CI")]
    [InlineData("File.OpenRead(path)")]
    [InlineData("File.OpenText(path)")]
    public Task Reports_WhenAStaticReadFactoryFollowsAWrite(string read)
    {
        return AnalyzerTestRunner.VerifyAsync<DiskRoundtripAnalyzer>($$"""
            using System.IO;
            class C
            {
                void M(string path, string value)
                {
                    File.WriteAllText(path, value);
                    using var reader = {|E128064:{{read}}|};
                }
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("FileMode.CreateNew")]
    [InlineData("FileMode.Append")]
    public Task Reports_WhenAWriteModePrecedesARead(string mode)
    {
        return AnalyzerTestRunner.VerifyAsync<DiskRoundtripAnalyzer>($$"""
            using System.IO;
            class C
            {
                string M(string path)
                {
                    using (var stream = new FileStream(path, {{mode}})) { }
                    return {|E128064:File.ReadAllText(path)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAReadWriteStreamPrecedesARead()
    {
        return AnalyzerTestRunner.VerifyAsync<DiskRoundtripAnalyzer>("""
            using System.IO;
            class C
            {
                string M(string path)
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite)) { }
                    return {|E128064:File.ReadAllText(path)|};
                }
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("if (flag) { File.WriteAllText(path, value); }")]
    [InlineData("while (flag) { File.WriteAllText(path, value); break; }")]
    [InlineData("for (int i = 0; i < 1; i++) { File.WriteAllText(path, value); }")]
    [InlineData("foreach (var item in new[] { 1 }) { File.WriteAllText(path, value); }")]
    [InlineData("switch (flag) { case true: File.WriteAllText(path, value); break; }")]
    [InlineData("try { } catch (IOException) { File.WriteAllText(path, value); }")]
    public Task Silent_WhenAConditionalWritePrecedesARead(string write)
    {
        return AnalyzerTestRunner.VerifyAsync<DiskRoundtripAnalyzer>($$"""
            using System.IO;
            class C
            {
                string M(string path, string value, bool flag)
                {
                    {{write}}
                    return File.ReadAllText(path);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnUnrelatedAssignmentFallsBetweenWriteAndRead()
    {
        return AnalyzerTestRunner.VerifyAsync<DiskRoundtripAnalyzer>("""
            using System.IO;
            class C
            {
                string M(string path, string other, string value)
                {
                    File.WriteAllText(path, value);
                    other = value;
                    return {|E128064:File.ReadAllText(path)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAPathAssignmentFollowsTheRead()
    {
        return AnalyzerTestRunner.VerifyAsync<DiskRoundtripAnalyzer>("""
            using System.IO;
            class C
            {
                string M(string path, string other, string value)
                {
                    File.WriteAllText(path, value);
                    var result = {|E128064:File.ReadAllText(path)|};
                    path = other;
                    return result;
                }
            }
            """);
    }
}
