using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskFromResultSyncIoCodeFixProviderBranchCoverageMoreTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<TaskFromResultSyncIoAnalyzer, TaskFromResultSyncIoCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            CompilerDiagnostics = CompilerDiagnostics.None,
            NumberOfFixAllIterations = 1
        }.RunAsync(TestContext.Current.CancellationToken);
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
