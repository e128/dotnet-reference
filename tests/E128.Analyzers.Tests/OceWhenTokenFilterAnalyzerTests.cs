using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OceWhenTokenFilterAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NegatedTokenFilter()
    {
        return AnalyzerTestRunner.VerifyAsync<OceWhenTokenFilterAnalyzer>("""
                           using System;
                           using System.Threading;
                           class C
                           {
                               void M(CancellationToken token)
                               {
                                   try
                                   {
                                   }
                                   {|E128100:catch (OperationCanceledException) when (!token.IsCancellationRequested)|}
                                   {
                                       throw;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_PositiveTokenIdiom()
    {
        return AnalyzerTestRunner.VerifyAsync<OceWhenTokenFilterAnalyzer>("""
                           using System;
                           using System.Threading;
                           class C
                           {
                               void M(CancellationToken token)
                               {
                                   try
                                   {
                                   }
                                   catch (OperationCanceledException) when (token.IsCancellationRequested)
                                   {
                                       throw;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_UnfilteredCatch()
    {
        return AnalyzerTestRunner.VerifyAsync<OceWhenTokenFilterAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try
                                   {
                                   }
                                   catch (OperationCanceledException)
                                   {
                                       throw;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoReport_UnrelatedFilter()
    {
        return AnalyzerTestRunner.VerifyAsync<OceWhenTokenFilterAnalyzer>("""
                           using System;
                           class C
                           {
                               void M(bool flag)
                               {
                                   try
                                   {
                                   }
                                   catch (OperationCanceledException) when (!flag)
                                   {
                                       throw;
                                   }
                               }
                           }
                           """);
    }
}
