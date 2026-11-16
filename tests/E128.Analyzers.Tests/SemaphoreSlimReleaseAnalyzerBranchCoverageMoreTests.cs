using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SemaphoreSlimReleaseAnalyzerBranchCoverageMoreTests
{
    private const string Header = """
                                  using System;
                                  using System.Threading;
                                  using System.Threading.Tasks;

                                  """;

    private static Task VerifyAsync(string source, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>(Header + source, expected: null, options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheWaitTakesATimeoutOrMoreThanOneArgument()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               async Task M(CancellationToken token)
                               {
                                   _sem.Wait(100);
                                   _sem.Wait(100, token);
                                   await _sem.WaitAsync(TimeSpan.FromSeconds(1));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheEarlyReturnCallsAMethodWithoutAnyRelease()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               static int Compute() => 1;

                               int M(bool flag)
                               {
                                   {|E128175:_sem.Wait()|};
                                   if (flag)
                                   {
                                       return Compute();
                                   }

                                   return 0;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnEarlyReturnFollowsARelease()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               int M(bool flag)
                               {
                                   _sem.Wait();
                                   if (flag)
                                   {
                                       _sem.Release();
                                       return 1;
                                   }

                                   try
                                   {
                                       return 0;
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
