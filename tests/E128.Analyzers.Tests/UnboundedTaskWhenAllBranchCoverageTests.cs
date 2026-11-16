using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the fan-out shapes of the unbounded WhenAll rule that the main suite leaves out.
/// </summary>
public sealed class UnboundedTaskWhenAllBranchCoverageTests
{
    private const string Header = """
                                  using System;
                                  using System.Collections.Generic;
                                  using System.Linq;
                                  using System.Threading.Tasks;

                                  """;

    private static Task VerifyAsync(string source, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<UnboundedTaskWhenAllAnalyzer>(Header + source, expected: null, options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnAsyncSelectIsMaterializedToAList()
    {
        return VerifyAsync("""
                           class C
                           {
                               static Task Work(int item) => Task.CompletedTask;

                               async Task M(IEnumerable<int> items)
                               {
                                   await {|E128037:Task.WhenAll(items.Select(async x => await Work(x)).ToList())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSelectorIsAMethodGroup()
    {
        return VerifyAsync("""
                           class C
                           {
                               static Task Work(int item) => Task.CompletedTask;

                               async Task M(IEnumerable<int> items)
                               {
                                   await Task.WhenAll(items.Select(Work));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSelectHasNoArguments()
    {
        return VerifyAsync(
            """
            class C
            {
                async Task M(IEnumerable<int> items)
                {
                    await Task.WhenAll(items.Select());
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheThrottleIsAnUnqualifiedCallInsideTheLambda()
    {
        return VerifyAsync("""
                           class C
                           {
                               static Task Work(int item) => Task.CompletedTask;

                               async Task M(IEnumerable<int> items)
                               {
                                   await Task.WhenAll(items.Select(async x => await Work(x)).Where(task => task != null));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsACallResult()
    {
        return VerifyAsync("""
                           class C
                           {
                               static IEnumerable<Task> Make() => Array.Empty<Task>();

                               async Task M()
                               {
                                   await Task.WhenAll(Make());
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsAFilteredSequence()
    {
        return VerifyAsync("""
                           class C
                           {
                               async Task M(IEnumerable<Task> tasks)
                               {
                                   await Task.WhenAll(tasks.Where(task => task.IsCompleted).ToArray());
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsAnArrayLiteral()
    {
        return VerifyAsync("""
                           class C
                           {
                               async Task M()
                               {
                                   await Task.WhenAll(new[] { Task.CompletedTask, Task.CompletedTask });
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsAParameter()
    {
        return VerifyAsync("""
                           class C
                           {
                               async Task M(IEnumerable<Task> tasks)
                               {
                                   await Task.WhenAll(tasks);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsFilledByOtherCalls()
    {
        return VerifyAsync("""
                           class C
                           {
                               static void Add(Task task)
                               {
                               }

                               async Task M(IReadOnlyList<int> items)
                               {
                                   var tasks = new List<Task>();
                                   foreach (var item in items)
                                   {
                                       Add(Task.Run(() => item));
                                       tasks.Insert(0, Task.Run(() => item));
                                       tasks.Add(Task.Delay(item));
                                       tasks.Add(Task.Factory.StartNew(() => item));
                                       tasks.Add(Task.CompletedTask);
                                       tasks.Add(Runner.Run(() => item));
                                   }

                                   await Task.WhenAll(tasks);
                               }
                           }

                           static class Runner
                           {
                               public static Task Run(Func<int> work) => Task.CompletedTask;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheRunLambdaWaitsOnALookAlikeSemaphore()
    {
        return VerifyAsync("""
                           namespace Other
                           {
                               public sealed class SemaphoreSlim
                               {
                                   public Task WaitAsync() => Task.CompletedTask;
                               }
                           }

                           class C
                           {
                               async Task M(IReadOnlyList<int> items, Other.SemaphoreSlim gate)
                               {
                                   var tasks = new List<Task>();
                                   foreach (var item in items)
                                   {
                                       tasks.Add({|E128037:Task.Run(async () => await gate.WaitAsync())|});
                                   }

                                   await Task.WhenAll(tasks);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheRunLambdaCallsAnUnqualifiedHelper()
    {
        return VerifyAsync("""
                           class C
                           {
                               static void Work(int item)
                               {
                               }

                               async Task M(IReadOnlyList<int> items)
                               {
                                   var tasks = new List<Task>();
                                   foreach (var item in items)
                                   {
                                       tasks.Add({|E128037:Task.Run(() => Work(item))|});
                                   }

                                   await Task.WhenAll(tasks);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheRunIsAddedThroughALocalFunctionAddCall()
    {
        return VerifyAsync("""
                           class C
                           {
                               async Task M(IReadOnlyList<int> items)
                               {
                                   var tasks = new List<Task>();
                                   void Add(Task task) => tasks.Add(task);
                                   foreach (var item in items)
                                   {
                                       Add(Task.Run(() => item));
                                   }

                                   await Task.WhenAll(tasks);
                               }
                           }
                           """);
    }
}
