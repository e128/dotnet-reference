using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
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

        var test = new CSharpCodeFixTest<
            TaskCompletionSourceOptionsAnalyzer,
            TaskCompletionSourceOptionsCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = Code,
            FixedCode = Unchanged,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.ExpectedDiagnostics.Add(new DiagnosticResult("E128159", DiagnosticSeverity.Warning).WithLocation(0));
        test.FixedState.ExpectedDiagnostics.Add(new DiagnosticResult("E128159", DiagnosticSeverity.Warning).WithLocation(8, 9));

        return test.RunAsync(TestContext.Current.CancellationToken);
    }
}
