using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskCompletionSourceOptionsCodeFixProviderBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheDiagnosticReportsASharedSetResultCall()
    {
        const string Code = """
            using System.Threading.Tasks;
            class C
            {
                private static readonly TaskCompletionSource<int> Shared = new(TaskCreationOptions.RunContinuationsAsynchronously);

                void M()
                {
                    {|#0:Shared.SetResult(1)|};
                }
            }
            """;

        const string Unchanged = """
            using System.Threading.Tasks;
            class C
            {
                private static readonly TaskCompletionSource<int> Shared = new(TaskCreationOptions.RunContinuationsAsynchronously);

                void M()
                {
                    Shared.SetResult(1);
                }
            }
            """;

        return CodeFixTestRunner.RunAsync<TaskCompletionSourceOptionsAnalyzer, TaskCompletionSourceOptionsCodeFixProvider>(
            Code,
            Unchanged,
            expected: [new DiagnosticResult("E128159", DiagnosticSeverity.Warning).WithLocation(0)],
            options: new CodeFixTestOptions { FixedExpected = [new DiagnosticResult("E128159", DiagnosticSeverity.Warning).WithLocation(8, 9)] });
    }
}
