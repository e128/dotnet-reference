using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskCompletionSourceOptionsAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { MarkupOptions = MarkupOptions.UseFirstDescriptor };

    [Fact]
    [Trait("Category", "CI")]
    public Task NoOptionsArgument_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   return {|E128159:new TaskCompletionSource<int>()|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EmptyOptionsArgument_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   return {|E128159:new TaskCompletionSource<int>(TaskCreationOptions.None)|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FlagPresent_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   return new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComputedOptions_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   TaskCreationOptions options = TaskCreationOptions.None;
                                   return new TaskCompletionSource<int>(options);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnrelatedCreation_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               object M()
                               {
                                   return new List<int>();
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetResultOnField_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly TaskCompletionSource<int> source =
                                   new(TaskCreationOptions.RunContinuationsAsynchronously);
                               void M()
                               {
                                   {|E128159:source.SetResult(1)|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetExceptionOnStaticField_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System;
                           using System.Threading.Tasks;
                           class C
                           {
                               private static readonly TaskCompletionSource<int> Source =
                                   new(TaskCreationOptions.RunContinuationsAsynchronously);
                               static void M()
                               {
                                   {|E128159:C.Source.SetException(new InvalidOperationException())|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetCanceledOnField_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly TaskCompletionSource<int> source =
                                   new(TaskCreationOptions.RunContinuationsAsynchronously);
                               void M()
                               {
                                   {|E128159:this.source.SetCanceled()|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetResultOnField_GuardedByInterlocked_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly TaskCompletionSource<int> source =
                                   new(TaskCreationOptions.RunContinuationsAsynchronously);
                               private int _completed;
                               void M(int value)
                               {
                                   if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
                                   {
                                       source.SetResult(value);
                                   }
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TrySetResultOnField_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               private readonly TaskCompletionSource<int> source =
                                   new(TaskCreationOptions.RunContinuationsAsynchronously);
                               void M()
                               {
                                   source.TrySetResult(1);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetResultOnLocal_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               void M()
                               {
                                   var source =
                                       new TaskCompletionSource<int>(
                                           TaskCreationOptions.RunContinuationsAsynchronously);
                                   source.SetResult(1);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetResultOnParameter_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               static void M(TaskCompletionSource<int> source)
                               {
                                   source.SetResult(1);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetResultOnProperty_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           using System.Threading.Tasks;
                           class C
                           {
                               private TaskCompletionSource<int> Source { get; } =
                                   new(TaskCreationOptions.RunContinuationsAsynchronously);
                               void M()
                               {
                                   Source.SetResult(1);
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetResultOnUserType_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<TaskCompletionSourceOptionsAnalyzer>("""
                           class Source
                           {
                               internal void SetResult(int value)
                               {
                               }
                           }

                           class C
                           {
                               private readonly Source source = new();
                               void M()
                               {
                                   source.SetResult(1);
                               }
                           }
                           """, options: Options);
    }
}
