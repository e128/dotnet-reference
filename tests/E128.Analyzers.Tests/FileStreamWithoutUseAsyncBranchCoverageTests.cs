using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileStreamWithoutUseAsyncBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenUseAsyncIsAParenthesizedFalse()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamWithoutUseAsyncAnalyzer>("""
            using System.IO;
            class C
            {
                static void M(string path)
                {
                    using var stream = {|E128190:new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, (false))|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenOptionsAreACastOfZero()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamWithoutUseAsyncAnalyzer>("""
            using System.IO;
            class C
            {
                static void M(string path)
                {
                    using var stream = {|E128190:new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, (FileOptions)0)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOptionsAreACastOfTheAsynchronousBit()
    {
        return AnalyzerTestRunner.VerifyAsync<FileStreamWithoutUseAsyncAnalyzer>("""
            using System.IO;
            class C
            {
                static void M(string path)
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, (FileOptions)0x40000000);
                }
            }
            """);
    }
}
