using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InRefStructE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<InRefStructE128Analyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InReadOnlySpan_Fires()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
            class C
            {
                void M() { }
            }
            """);
    }
}
