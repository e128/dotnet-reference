using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CatchFilterOceE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task CatchFilter_MissingOce_FiresE128039()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterOceAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try { }
                                   {|E128039:catch (Exception ex) when (ex is not OutOfMemoryException)|}
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchFilter_IncludesOce_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterOceAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try { }
                                   catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchFilter_IncludesTaskCanceledException_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterOceAnalyzer>("""
                           using System;
                           using System.Threading.Tasks;
                           class C
                           {
                               void M()
                               {
                                   try { }
                                   catch (Exception ex) when (ex is not OutOfMemoryException and not TaskCanceledException)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchFilter_PrecedingCatchHandlesOce_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterOceAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try { }
                                   catch (OperationCanceledException) { throw; }
                                   catch (Exception ex) when (ex is not OutOfMemoryException)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CatchFilter_NoFilterAtAll_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<CatchFilterOceAnalyzer>("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   try { }
                                   catch (Exception)
                                   {
                                   }
                               }
                           }
                           """);
    }
}
