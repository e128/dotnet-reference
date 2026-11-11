using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SemaphoreSlimReleaseAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task WaitAsyncWithFollowingTryFinally_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>("""
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);
                               async Task M()
                               {
                                   await _sem.WaitAsync();
                                   try
                                   {
                                       await Task.Yield();
                                   }
                                   finally
                                   {
                                       _sem.Release();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WaitAsyncWithNoTry_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>("""
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);
                               async Task M()
                               {
                                   await {|E128175:_sem.WaitAsync()|};
                                   await Task.Yield();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WaitWithNoArguments_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>("""
                           using System.Threading;
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);
                               void M()
                               {
                                   {|E128175:_sem.Wait()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WaitAsyncWithTryCatchAndNoFinally_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>("""
                           using System;
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);
                               async Task M()
                               {
                                   await {|E128175:_sem.WaitAsync()|};
                                   try
                                   {
                                       await Task.Yield();
                                   }
                                   catch (InvalidOperationException)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReleaseOnADifferentSemaphore_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>("""
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly SemaphoreSlim _first = new(1, 1);
                               private readonly SemaphoreSlim _second = new(1, 1);
                               async Task M()
                               {
                                   await {|E128175:_first.WaitAsync()|};
                                   try
                                   {
                                       await Task.Yield();
                                   }
                                   finally
                                   {
                                       _second.Release();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WaitWithTimeoutOverload_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>("""
                           using System.Threading;
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);
                               void M()
                               {
                                   if (!_sem.Wait(0))
                                   {
                                       return;
                                   }
                                   try
                                   {
                                   }
                                   finally
                                   {
                                       _sem.Release();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalSemaphoreWithFollowingTryFinally_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>("""
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M()
                               {
                                   using SemaphoreSlim sem = new(1, 1);
                                   await sem.WaitAsync(CancellationToken.None);
                                   try
                                   {
                                       await Task.Yield();
                                   }
                                   finally
                                   {
                                       sem.Release();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WaitInsideEnclosingTry_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>("""
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);
                               async Task M()
                               {
                                   try
                                   {
                                       await _sem.WaitAsync();
                                       await Task.Yield();
                                   }
                                   finally
                                   {
                                       _sem.Release();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnrelatedTypeWithWaitAndRelease_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>("""
                           class Gate
                           {
                               public void Wait() { }
                               public void Release() { }
                           }
                           class C
                           {
                               private readonly Gate _gate = new();
                               void M()
                               {
                                   _gate.Wait();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReleaseBeforeAnExitingReturn_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>("""
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);
                               async Task M(bool stop)
                               {
                                   await _sem.WaitAsync();
                                   if (stop)
                                   {
                                       _sem.Release();
                                       return;
                                   }
                                   try
                                   {
                                       await Task.Yield();
                                   }
                                   finally
                                   {
                                       _sem.Release();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReleaseUnderAConditionInFinally_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>("""
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);
                               async Task M(bool failed)
                               {
                                   await {|E128175:_sem.WaitAsync()|};
                                   try
                                   {
                                       await Task.Yield();
                                   }
                                   finally
                                   {
                                       if (failed)
                                       {
                                           _sem.Release();
                                       }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReturnBetweenWaitAndTry_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>("""
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);
                               async Task M(bool stop)
                               {
                                   await {|E128175:_sem.WaitAsync()|};
                                   if (stop)
                                   {
                                       return;
                                   }
                                   try
                                   {
                                       await Task.Yield();
                                   }
                                   finally
                                   {
                                       _sem.Release();
                                   }
                               }
                           }
                           """);
    }
}
