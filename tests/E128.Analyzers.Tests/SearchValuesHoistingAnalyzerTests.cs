using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SearchValuesHoistingAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<SearchValuesHoistingAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTableIsBuiltAtTheCallSite()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
    public Task Reports_WhenTheArgumentIsAStaticReadonlyField()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
