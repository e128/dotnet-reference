using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the creation and completion shapes of the TaskCompletionSource rule and its code fix.
/// </summary>
public sealed class TaskCompletionSourceOptionsBranchCoverageTests
{
    private static readonly AnalyzerTestOptions Options = new() { MarkupOptions = MarkupOptions.UseFirstDescriptor };

    private static Task VerifyAsync(string source, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>(source, expected: null, options ?? Options);
    }

    private static Task FixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<TaskCompletionSourceOptionsAnalyzer, TaskCompletionSourceOptionsCodeFixProvider>(
            source,
            fixedCode,
            markupOptions: MarkupOptions.UseFirstDescriptor);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompleterIsCalledWithoutAReceiver()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class MySource : TaskCompletionSource<int>
                           {
                               public MySource()
                                   : base(TaskCreationOptions.RunContinuationsAsynchronously)
                               {
                               }

                               void Finish()
                               {
                                   SetResult(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardInTheConditionIsNotAnInterlockedExchange()
    {
        return VerifyAsync("""
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly TaskCompletionSource<int> source =
                                   new(TaskCreationOptions.RunContinuationsAsynchronously);
                               private int _count;

                               bool Check() => true;

                               void M(int value)
                               {
                                   if (Check())
                                   {
                                       {|E128159:source.SetResult(value)|};
                                   }

                                   if (Interlocked.Increment(ref _count) == 1)
                                   {
                                       {|E128159:source.SetResult(value)|};
                                   }

                                   var local = 0;
                                   if (Interlocked.Exchange(ref local, 1) == 0)
                                   {
                                       {|E128159:source.SetResult(value)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCreationCannotBeBound()
    {
        return VerifyAsync(
            """
            using System.Threading.Tasks;
            class C
            {
                object M()
                {
                    return {|E128159:new TaskCompletionSource<int>(missing)|};
                }
            }
            """,
            new AnalyzerTestOptions
            {
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                CompilerDiagnostics = CompilerDiagnostics.None
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStateOverloadHasNoOptions()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   return {|E128159:new TaskCompletionSource<int>("state")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCreationHasOnlyAnInitializer()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   return {|E128159:new TaskCompletionSource<int> { }|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AddsTheOptionsAfterAStateArgument()
    {
        return FixAsync(
            """
            using System.Threading.Tasks;
            class C
            {
                object M()
                {
                    return {|E128159:new TaskCompletionSource<int>("state")|};
                }
            }
            """,
            """
            using System.Threading.Tasks;
            class C
            {
                object M()
                {
                    return new TaskCompletionSource<int>("state", TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ParenthesizesABinaryOptionsExpression()
    {
        return FixAsync(
            """
            using System.Threading.Tasks;
            class C
            {
                object M()
                {
                    return {|E128159:new TaskCompletionSource<int>(TaskCreationOptions.AttachedToParent | TaskCreationOptions.LongRunning)|};
                }
            }
            """,
            """
            using System.Threading.Tasks;
            class C
            {
                object M()
                {
                    return new TaskCompletionSource<int>((TaskCreationOptions.AttachedToParent | TaskCreationOptions.LongRunning) | TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ParenthesizesAConditionalOptionsExpression()
    {
        return FixAsync(
            """
            using System.Threading.Tasks;
            class C
            {
                object M()
                {
                    return {|E128159:new TaskCompletionSource<int>(true ? TaskCreationOptions.LongRunning : TaskCreationOptions.None)|};
                }
            }
            """,
            """
            using System.Threading.Tasks;
            class C
            {
                object M()
                {
                    return new TaskCompletionSource<int>((true ? TaskCreationOptions.LongRunning : TaskCreationOptions.None) | TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAnArgumentIsNamed()
    {
        return CodeFixTestRunner.NoFixAsync<TaskCompletionSourceOptionsAnalyzer, TaskCompletionSourceOptionsCodeFixProvider>(
            """
            using System.Threading.Tasks;
            class C
            {
                object M()
                {
                    return new TaskCompletionSource<int>(state: "state");
                }
            }
            """,
            expected: new DiagnosticResult("E128159", DiagnosticSeverity.Warning).WithSpan(6, 16, 6, 61));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheCreationHasNoArgumentList()
    {
        return CodeFixTestRunner.NoFixAsync<TaskCompletionSourceOptionsAnalyzer, TaskCompletionSourceOptionsCodeFixProvider>(
            """
            using System.Threading.Tasks;
            class C
            {
                object M()
                {
                    return new TaskCompletionSource<int> { };
                }
            }
            """,
            expected: new DiagnosticResult("E128159", DiagnosticSeverity.Warning).WithSpan(6, 16, 6, 49));
    }
}
