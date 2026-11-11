using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SearchValuesHoistingAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTableIsBuiltAtTheCallSite()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               bool M(ReadOnlySpan<char> text)
                               {
                                   return text.ContainsAny({|E128124:SearchValues.Create("abc")|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTableIsAStaticReadonlyField()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               private static readonly SearchValues<char> Values = SearchValues.Create("abc");

                               bool M(ReadOnlySpan<char> text)
                               {
                                   return text.ContainsAny(Values);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsAParameter()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               bool M(ReadOnlySpan<char> text, string chars)
                               {
                                   return text.ContainsAny(SearchValues.Create(chars));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentIsAnInstanceField()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               private readonly string _chars = "abc";

                               bool M(ReadOnlySpan<char> text)
                               {
                                   return text.ContainsAny(SearchValues.Create(_chars));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArgumentCallsAnInstanceMethod()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               private static readonly string Chars = "abc";

                               private string GetChars() => Chars;

                               bool M(ReadOnlySpan<char> text)
                               {
                                   return text.ContainsAny(SearchValues.Create(GetChars()));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheArgumentIsAStaticReadonlyField()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               private static readonly string Chars = "abc";

                               bool M(ReadOnlySpan<char> text)
                               {
                                   return text.ContainsAny({|E128124:SearchValues.Create(Chars)|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTableIsAStaticPropertyInitializer()
    {
        return AnalyzerTestRunner.VerifyAsync<SearchValuesHoistingAnalyzer>("""
                           using System;
                           using System.Buffers;
                           class C
                           {
                               private static SearchValues<char> Values { get; } = SearchValues.Create("abc");

                               bool M(ReadOnlySpan<char> text)
                               {
                                   return text.ContainsAny(Values);
                               }
                           }
                           """);
    }
}
