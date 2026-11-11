using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AlternateLookupAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheIndexerKeyIsAStringFromASpan()
    {
        return AnalyzerTestRunner.VerifyAsync<AlternateLookupAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               private readonly Dictionary<string, int> _cache = new();

                               int Lookup(ReadOnlySpan<char> key)
                               {
                                   return _cache[{|E128133:new string(key)|}];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenContainsKeyTakesAStringFromACharArray()
    {
        return AnalyzerTestRunner.VerifyAsync<AlternateLookupAnalyzer>("""
                           using System.Collections.Generic;

                           class C
                           {
                               private readonly HashSet<string> _names =
                                   new HashSet<string>(EqualityComparer<string>.Default);

                               bool Has(char[] key)
                               {
                                   return _names.Contains({|E128133:new string(key)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheComparerIsOrdinal()
    {
        return AnalyzerTestRunner.VerifyAsync<AlternateLookupAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               private readonly Dictionary<string, int> _cache = new(StringComparer.Ordinal);

                               int Lookup(ReadOnlySpan<char> key)
                               {
                                   return _cache[{|E128133:new string(key)|}];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheComparerIsCultureAware()
    {
        return AnalyzerTestRunner.VerifyAsync<AlternateLookupAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               private readonly Dictionary<string, int> _cache =
                                   new(StringComparer.CurrentCulture);

                               int Lookup(ReadOnlySpan<char> key)
                               {
                                   return _cache[new string(key)];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCollectionTypeIsUnknown()
    {
        return AnalyzerTestRunner.VerifyAsync<AlternateLookupAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               int Lookup(Dictionary<string, int> cache, ReadOnlySpan<char> key)
                               {
                                   return cache[new string(key)];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheKeyIsNotBuiltFromASpan()
    {
        return AnalyzerTestRunner.VerifyAsync<AlternateLookupAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               private readonly Dictionary<string, int> _cache = new();

                               int Lookup(int id)
                               {
                                   return _cache["item:" + id];
                               }
                           }
                           """);
    }
}
