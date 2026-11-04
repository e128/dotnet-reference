using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CollectionMutationInEnumerationCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_MaterializesLoopSource()
    {
        const string source = """
                              using System.Collections.Generic;

                              class C
                              {
                                  void M(List<int> items)
                                  {
                                      foreach (var item in {|E128155:items|})
                                      {
                                          items.Add(item);
                                      }
                                  }
                              }
                              """;

        const string fixedCode = """
                                 using System.Collections.Generic;

                                 class C
                                 {
                                     void M(List<int> items)
                                     {
                                         foreach (var item in items.ToArray())
                                         {
                                             items.Add(item);
                                         }
                                     }
                                 }
                                 """;

        return CodeFixTestRunner.RunAsync<CollectionMutationInEnumerationAnalyzer,
            CollectionMutationInEnumerationCodeFixProvider>(source, fixedCode);
    }
}
