using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ListGetRangeAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCalledOnList()
    {
        return AnalyzerTestRunner.VerifyAsync<ListGetRangeAnalyzer>("""
                           #nullable enable
                           using System.Collections.Generic;
                           class C
                           {
                               void M()
                               {
                                   var list = new List<int> { 1, 2, 3, 4, 5 };
                                   IReadOnlyList<int> range = {|E128084:list.GetRange(1, 3)|};
                               }
                           }
                           """);
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
}
