using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InMutableStructE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<InMutableStructE128Analyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InMutableStruct_Fires()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
                           using System.Threading;
                           class C
                           {
                               void M(in CancellationToken ct) { }
                           }
                           """);
    }
}
