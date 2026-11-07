using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class WhenAnyResultUnobservedAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected) =>
        AnalyzerTestRunner.VerifyAsync<WhenAnyResultUnobservedAnalyzer>(code, expected);

    [Fact]
    [Trait("Category", "CI")]
    public Task ExpressionStatement_DiscardedResult_Fires()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           class Example
                           {
                               void Run(Task first, Task second)
                               {
                                   {|E128183:Task.WhenAny(first, second)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalAssignedAndNeverRead_Fires()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           class Example
                           {
                               void Run(Task first, Task second)
                               {
                                   var winner = {|E128183:Task.WhenAny(first, second)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AwaitedDirectly_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           class Example
                           {
                               async Task RunAsync(Task first, Task second)
                               {
                                   await Task.WhenAny(first, second);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AwaitedThroughLocal_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           class Example
                           {
                               async Task RunAsync(Task first, Task second)
                               {
                                   var winner = Task.WhenAny(first, second);
                                   await winner;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReturnedAndYielded_DoNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class Example
                           {
                               Task Winner(Task first, Task second)
                               {
                                   return Task.WhenAny(first, second);
                               }

                               IEnumerable<Task> Winners(Task first, Task second)
                               {
                                   yield return Task.WhenAny(first, second);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StoredInFieldOrProperty_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           class Example
                           {
                               private Task pending = Task.CompletedTask;

                               private Task Pending { get; set; } = Task.CompletedTask;

                               void Run(Task first, Task second)
                               {
                                   pending = Task.WhenAny(first, second);
                                   Pending = Task.WhenAny(first, second);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PassedAsArgumentAndStoredInCollection_DoNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class Example
                           {
                               private readonly List<Task> tasks = new();

                               void Run(Task first, Task second)
                               {
                                   Track(Task.WhenAny(first, second));
                                   tasks.Add(Task.WhenAny(first, second));
                               }

                               static void Track(Task task)
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConsumedByWhenAll_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           class Example
                           {
                               async Task RunAsync(Task first, Task second)
                               {
                                   await Task.WhenAll(Task.WhenAny(first, second));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalReadByWhenAll_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           class Example
                           {
                               async Task RunAsync(Task first, Task second)
                               {
                                   var winner = Task.WhenAny(first, second);
                                   await Task.WhenAll(winner);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ChainedMemberAccess_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           class Example
                           {
                               void Run(Task first, Task second)
                               {
                                   Task.WhenAny(first, second).Wait();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DiscardAssignment_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           class Example
                           {
                               void Run(Task first, Task second)
                               {
                                   _ = Task.WhenAny(first, second);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OtherTypeWhenAny_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;

                           static class Helper
                           {
                               public static Task WhenAny(Task first, Task second) => first;
                           }

                           class Example
                           {
                               void Run(Task first, Task second)
                               {
                                   Helper.WhenAny(first, second);
                               }
                           }
                           """);
    }
}
