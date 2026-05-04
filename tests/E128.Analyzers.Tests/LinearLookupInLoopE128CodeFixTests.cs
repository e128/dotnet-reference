using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LinearLookupInLoopE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode, int fixAllIterations = 1)
    {
        return new CSharpCodeFixTest<LinearLookupInLoopAnalyzer, LinearLookupInLoopCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = fixAllIterations
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListContains_InForeach_ConvertsToHashSet()
    {
        const string source = """
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
                              """;

        const string fixedCode = """
                                 using System.Collections.Generic;
                                 using System.Linq;
                                 class C
                                 {
                                     void M()
                                     {
                                         var items = new List<int> { 1, 2, 3 };
                                         var lookup = new List<int> { 2, 3, 4 };
                                         var lookupSet = lookup.ToHashSet();
                                         foreach (var item in items)
                                         {
                                             if (lookupSet.Contains(item)) { }
                                         }
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListContains_InForLoop_ConvertsToHashSet()
    {
        const string source = """
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
                              """;

        const string fixedCode = """
                                 using System.Collections.Generic;
                                 using System.Linq;
                                 class C
                                 {
                                     void M()
                                     {
                                         var items = new List<int> { 1, 2, 3 };
                                         var lookup = new List<int> { 2, 3, 4 };
                                         var lookupSet = lookup.ToHashSet();
                                         for (int i = 0; i < items.Count; i++)
                                         {
                                             if (lookupSet.Contains(items[i])) { }
                                         }
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LinqAny_InForeach_ConvertsToHashSet()
    {
        const string source = """
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
                              """;

        const string fixedCode = """
                                 using System.Collections.Generic;
                                 using System.Linq;
                                 class C
                                 {
                                     void M()
                                     {
                                         var items = new List<int> { 1, 2, 3 };
                                         var lookup = new List<int> { 2, 3, 4 };
                                         var lookupSet = lookup.ToHashSet();
                                         foreach (var item in items)
                                         {
                                             if (lookupSet.Any(x => x == item)) { }
                                         }
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Contains_InWhereLambda_InsertsBeforeLinqCall()
    {
        const string source = """
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
                              """;

        const string fixedCode = """
                                 using System.Collections.Generic;
                                 using System.Linq;
                                 class C
                                 {
                                     void M()
                                     {
                                         var items = new List<int> { 1, 2, 3 };
                                         var lookup = new List<int> { 2, 3, 4 };
                                         var lookupSet = lookup.ToHashSet();
                                         var result = items.Where(x => lookupSet.Contains(x));
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Contains_AddsUsingSystemLinq_WhenMissing()
    {
        const string source = """
                              using System.Collections.Generic;
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
                              """;

        const string fixedCode = """
                                 using System.Collections.Generic;
                                 using System.Linq;
                                 class C
                                 {
                                     void M()
                                     {
                                         var items = new List<int> { 1, 2, 3 };
                                         var lookup = new List<int> { 2, 3, 4 };
                                         var lookupSet = lookup.ToHashSet();
                                         foreach (var item in items)
                                         {
                                             if (lookupSet.Contains(item)) { }
                                         }
                                     }
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IndexOf_InForeach_NoFixOffered()
    {
        const string source = """
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
                              """;

        return VerifyFixAsync(source, source, 0);
    }
}
