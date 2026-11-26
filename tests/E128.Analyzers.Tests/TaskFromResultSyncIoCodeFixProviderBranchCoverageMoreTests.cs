using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskFromResultSyncIoCodeFixProviderBranchCoverageMoreTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<TaskFromResultSyncIoAnalyzer, TaskFromResultSyncIoCodeFixProvider>(
            source,
            fixedCode,
            options: new CodeFixTestOptions
            {
                CompilerDiagnostics = CompilerDiagnostics.None,
                FixAllIterations = 1
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_LeavesAnUnresolvedCallThatSharesAMethodName()
    {
        return VerifyFixAsync(
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                Task<string> M(string path)
                {
                    var other = Missing.ReadAllText(path);
                    var text = File.ReadAllText(path);
                    return {|E128028:Task.FromResult(text)|};
                }
            }
            """,
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                async Task<string> M(string path)
                {
                    var other = Missing.ReadAllText(path);
                    var text = await File.ReadAllTextAsync(path);
                    return text;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_LeavesAFromResultCallWithoutExactlyOneArgument()
    {
        return VerifyFixAsync(
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                Task<string> M(string path, bool flag)
                {
                    if (flag)
                    {
                        return Task.FromResult();
                    }

                    var text = File.ReadAllText(path);
                    return {|E128028:Task.FromResult(text)|};
                }
            }
            """,
            """
            using System.IO;
            using System.Threading.Tasks;
            class C
            {
                async Task<string> M(string path, bool flag)
                {
                    if (flag)
                    {
                        return Task.FromResult();
                    }

                    var text = await File.ReadAllTextAsync(path);
                    return text;
                }
            }
            """);
    }
}
