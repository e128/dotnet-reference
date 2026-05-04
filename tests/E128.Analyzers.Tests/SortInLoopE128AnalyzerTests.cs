using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SortInLoopE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<SortInLoopAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListSort_InForeach_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 3, 1, 2 };
                                   var data = new List<string> { "c", "a", "b" };
                                   foreach (var item in items)
                                   {
                                       {|E128068:data.Sort()|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OrderBy_InForLoop_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 3, 1, 2 };
                                   for (int i = 0; i < 10; i++)
                                   {
                                       var sorted = {|E128068:items.OrderBy(x => x)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OrderByDescending_InWhileLoop_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 3, 1, 2 };
                                   int i = 0;
                                   while (i < 10)
                                   {
                                       var sorted = {|E128068:items.OrderByDescending(x => x)|};
                                       i++;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InsertAtZero_InForeach_Fires_E128069()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var result = new List<int>();
                                   var items = new List<int> { 1, 2, 3 };
                                   foreach (var item in items)
                                   {
                                       {|E128069:result.Insert(0, item)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InsertAtNonZero_InForeach_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var result = new List<int>();
                                   var items = new List<int> { 1, 2, 3 };
                                   foreach (var item in items)
                                   {
                                       result.Insert(1, item);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Sort_OutsideLoop_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 3, 1, 2 };
                                   items.Sort();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OrderBy_OutsideLoop_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 3, 1, 2 };
                                   var sorted = items.OrderBy(x => x);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ArraySort_InForLoop_Fires()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   var arr = new[] { 3, 1, 2 };
                                   for (int i = 0; i < 10; i++)
                                   {
                                       {|E128068:Array.Sort(arr)|};
                                   }
                               }
                           }
                           """);
    }
}
