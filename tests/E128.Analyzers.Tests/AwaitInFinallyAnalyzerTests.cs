using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AwaitInFinallyAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected) =>
        AnalyzerTestRunner.VerifyAsync<AwaitInFinallyAnalyzer>(code, expected);

    [Fact]
    [Trait("Category", "CI")]
    public Task Await_InFinally_Fires()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   try
                                   {
                                   }
                                   finally
                                   {
                                       {|E128186:await Task.Delay(1)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EveryAwait_InFinally_Fires()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   try
                                   {
                                   }
                                   finally
                                   {
                                       {|E128186:await Task.Delay(1)|};
                                       {|E128186:await Task.FromResult(2)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Await_InTryBlock_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   try
                                   {
                                       await Task.Delay(1);
                                   }
                                   finally
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Await_InCatchClause_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   try
                                   {
                                   }
                                   catch
                                   {
                                       await Task.Delay(1);
                                   }
                                   finally
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Await_InLocalFunctionDeclaredInFinally_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   try
                                   {
                                   }
                                   finally
                                   {
                                       async Task LocalAsync()
                                       {
                                           await Task.Delay(1);
                                       }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Await_InLambdaDeclaredInFinally_DoesNotFire()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   try
                                   {
                                   }
                                   finally
                                   {
                                       Func<Task> work = async () => await Task.Delay(1);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedFinally_InsideOuterFinally_ReportsOnce()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   try
                                   {
                                   }
                                   finally
                                   {
                                       try
                                       {
                                       }
                                       finally
                                       {
                                           {|E128186:await Task.Delay(1)|};
                                       }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Await_InOuterFinally_AfterNestedTry_Fires()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   try
                                   {
                                   }
                                   finally
                                   {
                                       try
                                       {
                                       }
                                       finally
                                       {
                                       }

                                       {|E128186:await Task.Delay(1)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Await_InNestedTryBlockInsideFinally_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   try
                                   {
                                   }
                                   finally
                                   {
                                       try
                                       {
                                           await Task.Delay(1);
                                       }
                                       catch
                                       {
                                       }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Await_InNestedCatchInsideFinally_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   try
                                   {
                                   }
                                   finally
                                   {
                                       try
                                       {
                                       }
                                       catch
                                       {
                                           await Task.Delay(1);
                                       }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AwaitUsingInFinally_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Threading.Tasks;
                           class C
                           {
                               static async Task M(IAsyncDisposable d)
                               {
                                   try
                                   {
                                   }
                                   finally
                                   {
                                       {|E128186:await|} using (d)
                                       {
                                       }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AwaitForeachInFinally_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               static async Task M(IAsyncEnumerable<int> items)
                               {
                                   try
                                   {
                                   }
                                   finally
                                   {
                                       {|E128186:await|} foreach (var item in items)
                                       {
                                       }
                                   }
                               }
                           }
                           """);
    }
}
