using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InMutableStructE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task InMutableStruct_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<InMutableStructE128Analyzer>("""
                           struct MutableStruct { public int X; }
                           class C
                           {
                               void M({|E128020:in MutableStruct value|}) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InGenericMutableStruct_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<InMutableStructE128Analyzer>("""
                           struct Batch<T> { public T[] Items; }
                           class Activity { }
                           class C
                           {
                               void M({|E128020:in Batch<Activity> batch|}) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InGenericWrapper_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<InMutableStructE128Analyzer>("""
                           struct Wrapper<T> { public T Value; }
                           class C
                           {
                               void M({|E128020:in Wrapper<string> w|}) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InMixedStruct_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<InMutableStructE128Analyzer>("""
                           struct MixedStruct { public readonly int X; public int Y; }
                           class C
                           {
                               void M({|E128020:in MixedStruct s|}) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReadonlyStruct_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<InMutableStructE128Analyzer>("""
                           readonly struct ImmutablePoint { public int X { get; } }
                           class C
                           {
                               void M(in ImmutablePoint p) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DateTime_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<InMutableStructE128Analyzer>("""
                           using System;
                           class C
                           {
                               void M(in DateTime dt) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RefStruct_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<InMutableStructE128Analyzer>("""
                           using System;
                           class C
                           {
                               void M(in ReadOnlySpan<char> text) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Primitive_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<InMutableStructE128Analyzer>("""
                           class C
                           {
                               void M(in int value) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Enum_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<InMutableStructE128Analyzer>("""
                           using System;
                           class C
                           {
                               void M(in DayOfWeek day) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WithoutInModifier_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<InMutableStructE128Analyzer>("""
                           struct MutableStruct { public int X; }
                           class C
                           {
                               void M(MutableStruct value) { }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CancellationToken_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<InMutableStructE128Analyzer>("""
                           using System.Threading;
                           class C
                           {
                               void M(in CancellationToken ct) { }
                           }
                           """);
    }
}
