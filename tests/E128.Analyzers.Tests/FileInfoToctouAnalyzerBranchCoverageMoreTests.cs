using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileInfoToctouAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheGuardedPathIsALiteral()
    {
        return AnalyzerTestRunner.VerifyAsync<FileInfoToctouAnalyzer>("""
            using System.IO;
            class C
            {
                string M()
                {
                    if (!File.Exists("data.txt")) return string.Empty;
                    return File.ReadAllText("data.txt");
                }
            }
            """);
    }
}
