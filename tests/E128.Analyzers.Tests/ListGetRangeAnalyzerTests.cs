using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ListGetRangeAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCalledOnList()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>(
            """
            #nullable enable
            using System.Collections.Generic;
            class C
            {
                void M()
                {
                    var list = new List<int> { 1, 2, 3, 4, 5 };
                    IReadOnlyList<int> range = {|#0:list.GetRange(1, 3)|};
                }
            }
            """,
            new DiagnosticResult("E128084", DiagnosticSeverity.Warning).WithLocation(0));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenResultStaysList()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var list = new List<int> { 1, 2, 3, 4, 5 };
                                   var inferred = list.GetRange(1, 3);
                                   inferred.Add(6);
                                   List<int> typed = list.GetRange(0, 2);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenReceiverMaybeNull()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>("""
                           #nullable enable
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<int>? list)
                               {
                                   IReadOnlyList<int> range = list.GetRange(1, 3);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNullableContextIsDisabled()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<int> list)
                               {
                                   IReadOnlyList<int> range = list.GetRange(1, 3);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenCalledOnNonList()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>("""
                           class MyCollection
                           {
                               public int[] GetRange(int start, int count) => new int[count];
                           }
                           class C
                           {
                               void M()
                               {
                                   var mc = new MyCollection();
                                   var range = mc.GetRange(0, 1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnIndexerLoopReadsAList()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>(
            """
            using System.Collections.Generic;
            class C
            {
                int Total(List<string> items)
                {
                    int total = 0;
                    {|#0:for (int i = 0; i < items.Count; i++)
                    {
                        total += items[i].Length;
                    }|}
                    return total;
                }
            }
            """,
            new DiagnosticResult("E128084", DiagnosticSeverity.Warning).WithLocation(0));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopMutatesTheList()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               int Total(List<string> items)
                               {
                                   int total = 0;
                                   for (int i = 0; i < items.Count; i++)
                                   {
                                       total += items[i].Length;
                                       items.Add("more");
                                   }
                                   return total;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopWritesThroughTheIndexer()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<string> items)
                               {
                                   for (int i = 0; i < items.Count; i++)
                                   {
                                       items[i] = "value";
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopVariableIndexesAnotherCollection()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<string> items, string[] other)
                               {
                                   for (int i = 0; i < items.Count; i++)
                                   {
                                       other[i] = items[i];
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLoopVariableHasAnotherUse()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<string> items)
                               {
                                   int total = 0;
                                   for (int i = 0; i < items.Count; i++)
                                   {
                                       total += items[i].Length + i;
                                   }
                               }
                           }
                           """);
    }
}
