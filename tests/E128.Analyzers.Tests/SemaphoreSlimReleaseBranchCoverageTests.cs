using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers the wait and release pairing shapes the main semaphore suite leaves out.
/// </summary>
public sealed class SemaphoreSlimReleaseBranchCoverageTests
{
    private const string Header = """
                                  using System;
                                  using System.Collections.Generic;
                                  using System.Threading;
                                  using System.Threading.Tasks;

                                  """;

    private static Task VerifyAsync(string source, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<SemaphoreSlimReleaseAnalyzer>(Header + source, expected: null, options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheWaitTakesACancellationToken()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M(CancellationToken token)
                               {
                                   {|E128175:_sem.Wait(token)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheWaitIsAFieldInitializer()
    {
        return VerifyAsync("""
                           class C
                           {
                               private static readonly SemaphoreSlim Sem = new(1, 1);

                               private static readonly Task Pending = {|E128175:Sem.WaitAsync()|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheWaitIsAssignedToAVariable()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M()
                               {
                                   Task pending = {|E128175:_sem.WaitAsync()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheWaitIsAnEmbeddedStatement()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M(bool flag)
                               {
                                   if (flag)
                                       {|E128175:_sem.Wait()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheWaitIsInsideAnExpressionBodiedLambda()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M()
                               {
                                   Task.Run(() => {|E128175:_sem.Wait()|});
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheWaitIsInsideABlockLambda()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M()
                               {
                                   Task.Run(() =>
                                   {
                                       {|E128175:_sem.Wait()|};
                                   });
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheWaitIsInsideALocalFunction_AndTheOuterTryReleases()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M()
                               {
                                   try
                                   {
                                       void Inner()
                                       {
                                           {|E128175:_sem.Wait()|};
                                       }

                                       Inner();
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
    public Task Reports_WhenTheWaitIsAnExpressionBodiedLocalFunction()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M()
                               {
                                   void Inner() => {|E128175:_sem.Wait()|};
                                   Inner();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnEnclosingTryHasOnlyACatch()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M()
                               {
                                   try
                                   {
                                       {|E128175:_sem.Wait()|};
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
    public Task Reports_WhenAThrowFollowsTheWait()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M()
                               {
                                   {|E128175:_sem.Wait()|};
                                   throw new InvalidOperationException();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAGotoFollowsTheWait()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M()
                               {
                                   {|E128175:_sem.Wait()|};
                                   goto Done;
                               Done:
                                   return;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAYieldFollowsTheWait()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               IEnumerable<int> M()
                               {
                                   {|E128175:_sem.Wait()|};
                                   yield return 1;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheOnlyReleaseComesAfterTheExit()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               int M(bool flag)
                               {
                                   {|E128175:_sem.Wait()|};
                                   if (flag)
                                   {
                                       return 1;
                                   }
                                   else
                                   {
                                       _sem.Release();
                                   }

                                   return 0;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnotherCallPrecedesTheExit()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               int M(bool flag)
                               {
                                   {|E128175:_sem.Wait()|};
                                   if (flag)
                                   {
                                       Console.WriteLine("exit");
                                       return 1;
                                   }

                                   return 0;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALaterLambdaReturnsBeforeThePairedTry()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M()
                               {
                                   _sem.Wait();
                                   Func<int> compute = () =>
                                   {
                                       return 1;
                                   };
                                   int local = 0;
                                   try
                                   {
                                       local = compute();
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
    public Task Silent_WhenTheWaitIsCalledWithoutAReceiver()
    {
        return VerifyAsync("""
                           class MySemaphore : SemaphoreSlim
                           {
                               public MySemaphore()
                                   : base(1)
                               {
                               }

                               void M()
                               {
                                   Wait();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheWaitReceiverHasNoSymbol()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(SemaphoreSlim first, SemaphoreSlim second)
                               {
                                   (first ?? second).Wait();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallDoesNotBind()
    {
        return VerifyAsync(
            """
            class C
            {
                void M(SemaphoreSlim sem)
                {
                    sem.Wait(unknown);
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFinallyReleasesThroughAnUnboundCall()
    {
        return VerifyAsync(
            """
            class C
            {
                private readonly SemaphoreSlim _sem = new(1, 1);

                void M(dynamic other)
                {
                    {|E128175:_sem.Wait()|};
                    try
                    {
                        Console.WriteLine();
                    }
                    finally
                    {
                        other.Release();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFinallyReleasesThroughAnUnqualifiedCall()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void Release()
                               {
                               }

                               void M()
                               {
                                   {|E128175:_sem.Wait()|};
                                   try
                                   {
                                       Console.WriteLine();
                                   }
                                   finally
                                   {
                                       Release();
                                       _sem.Dispose();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFinallyReleasesAReceiverWithoutASymbol()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M(SemaphoreSlim other)
                               {
                                   {|E128175:_sem.Wait()|};
                                   try
                                   {
                                       Console.WriteLine();
                                   }
                                   finally
                                   {
                                       (other ?? _sem).Release();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFinallyReleasesInsideALambda()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M()
                               {
                                   {|E128175:_sem.Wait()|};
                                   try
                                   {
                                       Console.WriteLine();
                                   }
                                   finally
                                   {
                                       Task.Run(() => _sem.Release());
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFinallyReleasesInsideALocalFunction()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M()
                               {
                                   {|E128175:_sem.Wait()|};
                                   try
                                   {
                                       Console.WriteLine();
                                   }
                                   finally
                                   {
                                       void Give() => _sem.Release();
                                   }
                               }
                           }
                           """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("var skipped = flag ? _sem.Release() : 0;")]
    [InlineData("switch (count) { case 1: _sem.Release(); break; }")]
    [InlineData("for (var i = 0; i < count; i++) { _sem.Release(); }")]
    [InlineData("foreach (var i in new[] { 1 }) { _sem.Release(); }")]
    [InlineData("while (flag) { _sem.Release(); flag = false; }")]
    [InlineData("do { _sem.Release(); } while (flag);")]
    public Task Reports_WhenTheReleaseInTheFinallyIsConditional(string releaseStatement)
    {
        return VerifyAsync($$"""
                             class C
                             {
                                 private readonly SemaphoreSlim _sem = new(1, 1);

                                 void M(bool flag, int count)
                                 {
                                     {|E128175:_sem.Wait()|};
                                     try
                                     {
                                         Console.WriteLine();
                                     }
                                     finally
                                     {
                                         {{releaseStatement}}
                                     }
                                 }
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReleaseIsAnUnconditionalNestedBlock()
    {
        return VerifyAsync("""
                           class C
                           {
                               private readonly SemaphoreSlim _sem = new(1, 1);

                               void M()
                               {
                                   _sem.Wait();
                                   try
                                   {
                                       Console.WriteLine();
                                   }
                                   finally
                                   {
                                       {
                                           _sem.Release();
                                       }
                                   }
                               }
                           }
                           """);
    }
}
