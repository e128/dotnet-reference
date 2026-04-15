using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InCancellationTokenE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<InCancellationTokenE128Analyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InCancellationToken_PublicMethod_FiresE128019()
    {
        return VerifyAsync("""
            using System.Threading;
            class C
            {
                void M({|E128019:in CancellationToken cancellationToken|}) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InCancellationToken_PrivateMethod_FiresE128019()
    {
        return VerifyAsync("""
            using System.Threading;
            class C
            {
                private void M({|E128019:in CancellationToken ct|}) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InCancellationToken_StaticMethod_FiresE128019()
    {
        return VerifyAsync("""
            using System.Threading;
            class C
            {
                static void M(string name, {|E128019:in CancellationToken cancellationToken|}) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InCancellationToken_WithDefault_FiresE128019()
    {
        return VerifyAsync("""
            using System.Threading;
            class C
            {
                void M({|E128019:in CancellationToken cancellationToken = default|}) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CancellationToken_ByValue_NoFire()
    {
        return VerifyAsync("""
            using System.Threading;
            class C
            {
                void M(CancellationToken cancellationToken) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CancellationToken_ByValueWithDefault_NoFire()
    {
        return VerifyAsync("""
            using System.Threading;
            class C
            {
                void M(CancellationToken cancellationToken = default) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InOtherStruct_NoFire()
    {
        return VerifyAsync("""
            struct BigStruct { public long A, B, C, D; }
            class C
            {
                void M(in BigStruct s) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoCancellationToken_NoFire()
    {
        return VerifyAsync("""
            class C
            {
                void M(string name) { }
            }
            """);
    }
}
