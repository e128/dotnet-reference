using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InCancellationTokenE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task InCancellationToken_PublicMethod_FiresE128019()
    {
        return AnalyzerTestRunner.VerifyAsync<InCancellationTokenE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<InCancellationTokenE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<InCancellationTokenE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<InCancellationTokenE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<InCancellationTokenE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<InCancellationTokenE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<InCancellationTokenE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<InCancellationTokenE128Analyzer>("""
                           class C
                           {
                               void M(string name) { }
                           }
                           """);
    }
}
