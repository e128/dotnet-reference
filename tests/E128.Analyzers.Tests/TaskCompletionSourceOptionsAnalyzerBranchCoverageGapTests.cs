using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskCompletionSourceOptionsAnalyzerBranchCoverageGapTests
{
    private static readonly AnalyzerTestOptions NoCompilerDiagnostics = new() { MarkupOptions = MarkupOptions.UseFirstDescriptor, CompilerDiagnostics = CompilerDiagnostics.None };

    private static Task VerifyAsync(string source, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>(
            source,
            expected: null,
            options ?? new AnalyzerTestOptions { MarkupOptions = MarkupOptions.UseFirstDescriptor });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenSetResultBelongsToAnotherType()
    {
        return VerifyAsync("""
            class Signal
            {
                public void SetResult(int value) { }
            }

            class C
            {
                private readonly Signal _signal = new();

                void M()
                {
                    _signal.SetResult(1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverTypeCannotBeBound()
    {
        return VerifyAsync(
            """
            class C
            {
                private readonly Missing _source;

                void M()
                {
                    _source.SetResult(1);
                }
            }
            """,
            NoCompilerDiagnostics);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnUnboundCallSitsInTheGuardCondition()
    {
        return VerifyAsync(
            """
            using System.Threading.Tasks;
            class C
            {
                private readonly TaskCompletionSource<int> _source =
                    new(TaskCreationOptions.RunContinuationsAsynchronously);

                void M(int value)
                {
                    if (Missing() == 0)
                    {
                        {|E128159:_source.SetResult(value)|};
                    }
                }
            }
            """,
            NoCompilerDiagnostics);
    }
}
