using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ImmutableArrayCreateToArrayAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCreateWrapsToArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ImmutableArrayCreateToArrayAnalyzer>("""
                           using System.Collections.Generic;
                           using System.Collections.Immutable;
                           class C
                           {
                               void M()
                               {
                                   var list = new List<int> { 1, 2, 3 };
                                   var arr = {|E128083:ImmutableArray.Create(list.ToArray())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCreateRangeWrapsToArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ImmutableArrayCreateToArrayAnalyzer>("""
                           using System.Collections.Generic;
                           using System.Collections.Immutable;
                           class C
                           {
                               void M()
                               {
                                   var list = new List<int> { 1, 2, 3 };
                                   var arr = {|E128083:ImmutableArray.CreateRange(list.ToArray())|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenCreateWrapsLocalArray()
    {
        return AnalyzerTestRunner.VerifyAsync<ImmutableArrayCreateToArrayAnalyzer>("""
                           using System.Collections.Immutable;
                           class C
                           {
                               void M()
                               {
                                   var data = new int[] { 1, 2, 3 };
                                   var arr = ImmutableArray.Create(data);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenToArrayIsUserDefined()
    {
        return AnalyzerTestRunner.VerifyAsync<ImmutableArrayCreateToArrayAnalyzer>("""
                           using System.Collections.Immutable;
                           class Cache
                           {
                               private readonly int[] _shared = { 1, 2, 3 };
                               public int[] ToArray() => _shared;
                           }
                           class C
                           {
                               void M(Cache cache)
                               {
                                   var arr = ImmutableArray.Create(cache.ToArray());
                               }
                           }
                           """);
    }
}
