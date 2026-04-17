using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcurrencyLimitE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConcurrencyLimitAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SemaphoreSlim_ZeroInitialCount_Fires()
    {
        return VerifyAsync("""
                           using System.Threading;
                           class C
                           {
                               void M()
                               {
                                   var s = {|E128040:new SemaphoreSlim(0)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SemaphoreSlim_NegativeCount_Fires()
    {
        return VerifyAsync("""
                           using System.Threading;
                           class C
                           {
                               void M()
                               {
                                   var s = {|E128040:new SemaphoreSlim(-5)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SemaphoreSlim_PositiveCount_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.Threading;
                           class C
                           {
                               void M()
                               {
                                   var s = new SemaphoreSlim(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParallelOptions_ZeroDegree_Fires()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               void M()
                               {
                                   var options = new ParallelOptions { MaxDegreeOfParallelism = {|E128040:0|} };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParallelOptions_MinusOne_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               void M()
                               {
                                   var options = new ParallelOptions { MaxDegreeOfParallelism = -1 };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ChannelCreateBounded_Zero_Fires()
    {
        return VerifyAsync("""
                           using System.Threading.Channels;
                           class C
                           {
                               void M()
                               {
                                   var ch = {|E128040:Channel.CreateBounded<int>(0)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ChannelCreateBounded_Positive_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.Threading.Channels;
                           class C
                           {
                               void M()
                               {
                                   var ch = Channel.CreateBounded<int>(10);
                               }
                           }
                           """);
    }
}
