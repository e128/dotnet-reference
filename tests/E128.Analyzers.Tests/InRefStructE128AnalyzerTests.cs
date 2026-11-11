using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InRefStructE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task InReadOnlySpan_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<InRefStructE128Analyzer>("""
                           using System;
                           class C
                           {
                               void M({|E128021:in ReadOnlySpan<char> text|}) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InSpan_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<InRefStructE128Analyzer>("""
                           using System;
                           class C
                           {
                               void M({|E128021:in Span<byte> data|}) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InReadOnlySpanByte_StaticMethod_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<InRefStructE128Analyzer>("""
                           using System;
                           class C
                           {
                               static string Convert({|E128021:in ReadOnlySpan<byte> data|}) => string.Empty;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReadOnlySpan_ByValue_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<InRefStructE128Analyzer>("""
                           using System;
                           class C
                           {
                               void M(ReadOnlySpan<char> text) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InRegularStruct_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<InRefStructE128Analyzer>("""
                           readonly struct BigData { public readonly long A, B, C, D; }
                           class C
                           {
                               void M(in BigData d) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoParameters_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<InRefStructE128Analyzer>("""
                           class C
                           {
                               void M() { }
                           }
                           """);
    }
}
