using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LinearLookupInLoopE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<LinearLookupInLoopAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListContains_InForeach_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   var lookup = new List<int> { 2, 3, 4 };
                                   foreach (var item in items)
                                   {
                                       if ({|E128066:lookup.Contains(item)|}) { }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListContains_InForLoop_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   var lookup = new List<int> { 2, 3, 4 };
                                   for (int i = 0; i < items.Count; i++)
                                   {
                                       if ({|E128066:lookup.Contains(items[i])|}) { }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListContains_InWhileLoop_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   var lookup = new List<int> { 2, 3, 4 };
                                   int i = 0;
                                   while (i < items.Count)
                                   {
                                       if ({|E128066:lookup.Contains(items[i])|}) { }
                                       i++;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoWhileLoop_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   var lookup = new List<int> { 2, 3, 4 };
                                   int i = 0;
                                   do
                                   {
                                       if ({|E128066:lookup.Contains(items[i])|}) { }
                                       i++;
                                   } while (i < items.Count);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LinqAny_InForeach_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   var lookup = new List<int> { 2, 3, 4 };
                                   foreach (var item in items)
                                   {
                                       if ({|E128066:lookup.Any(x => x == item)|}) { }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListIndexOf_InForeach_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<string> { "a", "b", "c" };
                                   var lookup = new List<string> { "b", "c", "d" };
                                   foreach (var item in items)
                                   {
                                       var idx = {|E128066:lookup.IndexOf(item)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListRemove_InForeach_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   var lookup = new List<int> { 2, 3, 4 };
                                   foreach (var item in items)
                                   {
                                       {|E128066:lookup.Remove(item)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LinqContains_InWhereLambda_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   var lookup = new List<int> { 2, 3, 4 };
                                   var result = items.Where(x => {|E128066:lookup.Contains(x)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LinqContains_InSelectLambda_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   var lookup = new List<int> { 2, 3, 4 };
                                   var result = items.Select(x => {|E128066:lookup.Contains(x)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LinqAny_InWhereLambda_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   var lookup = new List<int> { 2, 3, 4 };
                                   var result = items.Where(x => {|E128066:lookup.Any(y => y == x)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ArrayContains_InForeach_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   var lookup = new[] { 2, 3, 4 };
                                   foreach (var item in items)
                                   {
                                       if ({|E128066:lookup.Contains(item)|}) { }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IEnumerableContains_InForeach_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M(IEnumerable<int> lookup)
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   foreach (var item in items)
                                   {
                                       if ({|E128066:lookup.Contains(item)|}) { }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HashSetContains_InForeach_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   var lookup = new HashSet<int> { 2, 3, 4 };
                                   foreach (var item in items)
                                   {
                                       if (lookup.Contains(item)) { }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DictionaryContainsKey_InForeach_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   var lookup = new Dictionary<int, string> { { 2, "a" } };
                                   foreach (var item in items)
                                   {
                                       if (lookup.ContainsKey(item)) { }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ContainsOutsideLoop_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var lookup = new List<int> { 2, 3, 4 };
                                   var result = lookup.Contains(2);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantBoundForLoop_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var lookup = new List<int> { 2, 3, 4 };
                                   for (int i = 0; i < 3; i++)
                                   {
                                       if (lookup.Contains(i)) { }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ISetContains_InForeach_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M(ISet<int> lookup)
                               {
                                   var items = new List<int> { 1, 2, 3 };
                                   foreach (var item in items)
                                   {
                                       if (lookup.Contains(item)) { }
                                   }
                               }
                           }
                           """);
    }
}
