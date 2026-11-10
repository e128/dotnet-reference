using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TaskCompletionSourceOptionsAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TaskCompletionSourceOptionsAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            MarkupOptions = MarkupOptions.UseFirstDescriptor
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoOptionsArgument_Fires()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   return {|E128159:new TaskCompletionSource<int>()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EmptyOptionsArgument_Fires()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   return {|E128159:new TaskCompletionSource<int>(TaskCreationOptions.None)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FlagPresent_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   return new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComputedOptions_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               object M()
                               {
                                   TaskCreationOptions options = TaskCreationOptions.None;
                                   return new TaskCompletionSource<int>(options);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UnrelatedCreation_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           class C
                           {
                               object M()
                               {
                                   return new List<int>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetResultOnField_Fires()
    {
        return VerifyAsync("""
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetExceptionOnStaticField_Fires()
    {
        return VerifyAsync("""
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetCanceledOnField_Fires()
    {
        return VerifyAsync("""
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetResultOnField_GuardedByInterlocked_DoesNotFire()
    {
        return VerifyAsync("""
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TrySetResultOnField_DoesNotFire()
    {
        return VerifyAsync("""
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetResultOnLocal_DoesNotFire()
    {
        return VerifyAsync("""
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetResultOnParameter_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               static void M(TaskCompletionSource<int> source)
                               {
                                   source.SetResult(1);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetResultOnProperty_DoesNotFire()
    {
        return VerifyAsync("""
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
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SetResultOnUserType_DoesNotFire()
    {
        return VerifyAsync("""
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
                           """);
    }
}
