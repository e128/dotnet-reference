using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnboundedTaskWhenAllAnalyzerBranchCoverageGapTests
{
    private const string Header = """
                                  using System;
                                  using System.Collections.Generic;
                                  using System.Linq;
                                  using System.Threading;
                                  using System.Threading.Tasks;

                                  """;

    private static Task VerifyAsync(string source, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<UnboundedTaskWhenAllAnalyzer>(Header + source, expected: null, options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheAsyncSelectorIsAParenthesizedLambda()
    {
        return VerifyAsync("""
                           class C
                           {
                               static Task Work(int item) => Task.CompletedTask;

                               async Task M(IEnumerable<int> items)
                               {
                                   await {|E128037:Task.WhenAll(items.Select(async (x) => await Work(x)))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenASelectMethodTakesNoArguments()
    {
        return VerifyAsync("""
                           class Source
                           {
                               public IEnumerable<Task> Select() => new List<Task>();
                           }

                           class C
                           {
                               async Task M(Source source)
                               {
                                   await Task.WhenAll(source.Select());
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSelectorReturnsATypeParameter()
    {
        return VerifyAsync("""
                           class C
                           {
                               async Task M<T>(IEnumerable<int> items, Func<int, T> make)
                                   where T : Task
                               {
                                   await Task.WhenAll(items.Select(x => make(x)));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRunCallIsUnqualified()
    {
        return VerifyAsync("""
                           class C
                           {
                               static Task Run(Func<int> work) => Task.CompletedTask;

                               async Task M(IReadOnlyList<int> items)
                               {
                                   var tasks = new List<Task>();
                                   foreach (var item in items)
                                   {
                                       tasks.Add(Run(() => item));
                                   }

                                   await Task.WhenAll(tasks);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRunCallDoesNotBind()
    {
        return VerifyAsync(
            """
            class C
            {
                async Task M(IReadOnlyList<int> items)
                {
                    var tasks = new List<Task>();
                    foreach (var item in items)
                    {
                        tasks.Add(Task.Run(1, 2, 3));
                    }

                    await Task.WhenAll(tasks);
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheWaitAsyncCallIsDynamic()
    {
        return VerifyAsync(
            """
            class C
            {
                async Task M(IReadOnlyList<int> items, dynamic gate)
                {
                    var tasks = new List<Task>();
                    foreach (var item in items)
                    {
                        tasks.Add({|E128037:Task.Run(async () => await gate.WaitAsync())|});
                    }

                    await Task.WhenAll(tasks);
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}
