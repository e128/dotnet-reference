using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnboundedTaskWhenAllE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<UnboundedTaskWhenAllAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_AsyncSelect_NoThrottle_FiresE128037()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   var items = new List<string> { "a", "b" };
                                   await {|E128037:Task.WhenAll(items.Select(async x => await Task.Delay(1)))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_AsyncSelect_WithSemaphore_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               private static readonly SemaphoreSlim _sem = new(5);
                               async Task M()
                               {
                                   var items = new List<string> { "a", "b" };
                                   await Task.WhenAll(items.Select(async x =>
                                   {
                                       await _sem.WaitAsync();
                                       try { await Task.Delay(1); }
                                       finally { _sem.Release(); }
                                   }));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_AsyncSelectThroughToArray_NoThrottle_FiresE128037()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   var items = new List<string> { "a", "b" };
                                   await {|E128037:Task.WhenAll(items.Select(async x => await Task.Delay(1)).ToArray())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_AsyncSelectThroughToArray_WithSemaphore_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               private static readonly SemaphoreSlim _sem = new(5);
                               async Task M()
                               {
                                   var items = new List<string> { "a", "b" };
                                   await Task.WhenAll(items.Select(async x =>
                                   {
                                       await _sem.WaitAsync();
                                       try { await Task.Delay(1); }
                                       finally { _sem.Release(); }
                                   }).ToList());
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_PlainLambdaReturningTask_FiresE128037()
    {
        // The fan-out is unbounded whether or not the lambda carries the async keyword.
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   var items = new List<string> { "a", "b" };
                                   await {|E128037:Task.WhenAll(items.Select(x => Task.Delay(1)))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_TaskRunListInLoop_FiresE128037()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M(IReadOnlyList<string> paths)
                               {
                                   List<Task> tasks = new();
                                   foreach (string path in paths)
                                   {
                                       tasks.Add({|E128037:Task.Run(() => path.Length)|});
                                   }

                                   await Task.WhenAll(tasks);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_TaskRunWithoutLoop_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   List<Task> tasks = new();
                                   tasks.Add(Task.Run(() => 1));
                                   await Task.WhenAll(tasks);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_TaskRunListWithSemaphore_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M(IReadOnlyList<string> paths, SemaphoreSlim gate)
                               {
                                   List<Task> tasks = new();
                                   foreach (string path in paths)
                                   {
                                       tasks.Add(Task.Run(async () =>
                                       {
                                           await gate.WaitAsync();
                                           await Task.Delay(path.Length);
                                       }));
                                   }

                                   await Task.WhenAll(tasks);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_TaskRunInBoundedLoop_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   List<Task> tasks = new();
                                   for (int i = 0; i < 3; i++)
                                   {
                                       tasks.Add(Task.Run(() => i));
                                   }

                                   await Task.WhenAll(tasks);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_TaskRunWithWaitAsyncTimeout_FiresE128037()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M(IReadOnlyList<string> paths)
                               {
                                   List<Task> tasks = new();
                                   foreach (string path in paths)
                                   {
                                       tasks.Add({|E128037:Task.Run(async () => await Task.Delay(path.Length).WaitAsync(TimeSpan.FromSeconds(5)))|});
                                   }

                                   await Task.WhenAll(tasks);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhenAll_MultipleArgs_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   await Task.WhenAll(Task.Delay(1), Task.Delay(2));
                               }
                           }
                           """);
    }
}
