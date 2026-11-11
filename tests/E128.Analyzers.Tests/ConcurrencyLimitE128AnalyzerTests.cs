using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcurrencyLimitE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task SemaphoreSlim_ZeroInitialCount_DoesNotFire()
    {
        // Zero is a legal initial count, so a gate that starts closed is not a defect.
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
                           using System.Threading;
                           class C
                           {
                               void M()
                               {
                                   var s = new SemaphoreSlim(0);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SemaphoreSlim_ZeroInitialCountWithMaxCount_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
                           using System.Threading;
                           class C
                           {
                               void M()
                               {
                                   var s = new SemaphoreSlim(0, 4);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MaxDegreeOfParallelism_OnAReceiver_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               void M(ParallelOptions options)
                               {
                                   options.MaxDegreeOfParallelism = {|E128040:0|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SemaphoreSlim_NegativeCount_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
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
