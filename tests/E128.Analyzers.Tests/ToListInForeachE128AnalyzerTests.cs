using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ToListInForeachE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task ToListInForeach_DirectCall_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ToListInForeachAnalyzer>("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M(IEnumerable<int> items)
                               {
                                   foreach (var x in {|E128018:items.ToList()|})
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToListInForeach_QueryableSource_Fires()
    {
        // The call binds to Enumerable.ToList, because IQueryable<T> converts to IEnumerable<T>.
        return AnalyzerTestRunner.VerifyAsync<ToListInForeachAnalyzer>("""
                           using System.Linq;
                           class C
                           {
                               void M(IQueryable<int> items)
                               {
                                   foreach (var x in {|E128018:items.ToList()|})
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToListInForeach_WithLinqChain_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ToListInForeachAnalyzer>("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M(IEnumerable<int> items)
                               {
                                   foreach (var x in {|E128018:items.Where(i => i > 0).ToList()|})
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToArrayInForeach_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ToListInForeachAnalyzer>("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M(IEnumerable<int> items)
                               {
                                   foreach (var x in items.ToArray())
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToListOnANonLinqType_DoesNotFire()
    {
        // A domain type with its own ToList() is not Enumerable.ToList, so the allocation advice
        // does not apply to it.
        return AnalyzerTestRunner.VerifyAsync<ToListInForeachAnalyzer>("""
                           using System.Collections.Generic;
                           class Bag
                           {
                               public List<int> ToList() => [];
                           }
                           class C
                           {
                               void M(Bag bag)
                               {
                                   foreach (var x in bag.ToList())
                                   {
                                   }
                               }
                           }
                           """);
    }
}
