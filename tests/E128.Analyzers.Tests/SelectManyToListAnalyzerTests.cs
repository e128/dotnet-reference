using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SelectManyToListAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenSelectManyFollowedByToList()
    {
        return AnalyzerTestRunner.VerifyAsync<SelectManyToListAnalyzer>("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var lists = new List<List<int>> { new() { 1, 2 }, new() { 3, 4 } };
                                   var flat = {|E128085:lists.SelectMany(x => x).ToList()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenSelectManyNotFollowedByToList()
    {
        return AnalyzerTestRunner.VerifyAsync<SelectManyToListAnalyzer>("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var lists = new List<List<int>> { new() { 1, 2 }, new() { 3, 4 } };
                                   var flat = lists.SelectMany(x => x).ToArray();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenToListWithoutSelectMany()
    {
        return AnalyzerTestRunner.VerifyAsync<SelectManyToListAnalyzer>("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M()
                               {
                                   var list = new List<int> { 1, 2, 3 };
                                   var copy = list.ToList();
                               }
                           }
                           """);
    }
}
