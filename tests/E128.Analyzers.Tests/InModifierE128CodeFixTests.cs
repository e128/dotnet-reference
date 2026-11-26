using System.Threading.Tasks;
using E128.Analyzers.Design;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InModifierE128CodeFixTests
{
    private static Task VerifyCancellationTokenFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<InCancellationTokenE128Analyzer, InModifierCodeFixProvider>(source, fixedCode);
    }

    private static Task VerifyMutableStructFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<InMutableStructE128Analyzer, InModifierCodeFixProvider>(source, fixedCode);
    }

    private static Task VerifyRefStructFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<InRefStructE128Analyzer, InModifierCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RemovesInFromCancellationToken()
    {
        return VerifyCancellationTokenFixAsync(
            """
            using System.Threading;
            class C
            {
                void M({|E128019:in CancellationToken cancellationToken|}) { }
            }
            """,
            """
            using System.Threading;
            class C
            {
                void M(CancellationToken cancellationToken) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RemovesInFromMutableStruct()
    {
        return VerifyMutableStructFixAsync(
            """
            struct MutableStruct { public int X; }
            class C
            {
                void M({|E128020:in MutableStruct value|}) { }
            }
            """,
            """
            struct MutableStruct { public int X; }
            class C
            {
                void M(MutableStruct value) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RemovesInFromRefStruct()
    {
        return VerifyRefStructFixAsync(
            """
            using System;
            class C
            {
                void M({|E128021:in ReadOnlySpan<char> text|}) { }
            }
            """,
            """
            using System;
            class C
            {
                void M(ReadOnlySpan<char> text) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RemovesIn_WithMultipleParameters()
    {
        return VerifyCancellationTokenFixAsync(
            """
            using System.Threading;
            class C
            {
                void M(string name, {|E128019:in CancellationToken ct|}) { }
            }
            """,
            """
            using System.Threading;
            class C
            {
                void M(string name, CancellationToken ct) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RemovesIn_WithDefaultValue()
    {
        return VerifyCancellationTokenFixAsync(
            """
            using System.Threading;
            class C
            {
                void M({|E128019:in CancellationToken ct = default|}) { }
            }
            """,
            """
            using System.Threading;
            class C
            {
                void M(CancellationToken ct = default) { }
            }
            """);
    }
}
