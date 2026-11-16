using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskFromResultSyncIoAnalyzerBranchCoverageGapTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMethodAwaitsBeforeItReturnsAFromResultTask()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskFromResultSyncIoAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                async Task<object> M(string path)
                {
                    await Task.Delay(1);
                    File.WriteAllText(path, "x");
                    return Task.FromResult(1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFromResultCallDoesNotBind()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskFromResultSyncIoAnalyzer>(
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                Task<int> M(string path)
                {
                    File.WriteAllText(path, "x");
                    return Task.FromResult();
                }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}
