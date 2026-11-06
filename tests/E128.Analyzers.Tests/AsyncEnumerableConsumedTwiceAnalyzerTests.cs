using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncEnumerableConsumedTwiceAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<AsyncEnumerableConsumedTwiceAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParameterEnumeratedTwice_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(IAsyncEnumerable<int> source)
                               {
                                   await foreach (var item in source)
                                   {
                                   }

                                   await foreach (var item in {|E128182:source|})
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EnumeratedInSeparateBranches_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(IAsyncEnumerable<int> source, bool first)
                               {
                                   if (first)
                                   {
                                       await foreach (var item in source)
                                       {
                                       }
                                   }
                                   else
                                   {
                                       await foreach (var item in source)
                                       {
                                       }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReassignedBetweenEnumerations_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               static IAsyncEnumerable<int> Create() => default!;

                               async Task M()
                               {
                                   var source = Create();
                                   await foreach (var item in source)
                                   {
                                   }

                                   source = Create();
                                   await foreach (var item in source)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalEnumeratedTwice_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               private static async IAsyncEnumerable<int> Build()
                               {
                                   yield return 1;
                                   await Task.Yield();
                               }

                               private async Task M()
                               {
                                   IAsyncEnumerable<int> local = Build();

                                   await foreach (var item in local)
                                   {
                                   }

                                   await foreach (var item in {|E128182:local|})
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FieldViaThisEnumeratedTwice_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               private readonly IAsyncEnumerable<int> _source = Build();

                               private static async IAsyncEnumerable<int> Build()
                               {
                                   yield return 1;
                                   await Task.Yield();
                               }

                               private async Task M()
                               {
                                   await foreach (var item in _source)
                                   {
                                   }

                                   await foreach (var item in {|E128182:this._source|})
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AwaitForeachThenGetAsyncEnumerator_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(IAsyncEnumerable<int> source)
                               {
                                   await foreach (var item in source)
                                   {
                                   }

                                   await using var enumerator = {|E128182:source|}.GetAsyncEnumerator();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WithCancellationWrapperEnumeratedTwice_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(IAsyncEnumerable<int> source)
                               {
                                   await foreach (var item in source.WithCancellation(default))
                                   {
                                   }

                                   await foreach (var item in {|E128182:source|}.WithCancellation(default))
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThreeEnumerations_ReportsAtSecondAndThird()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(IAsyncEnumerable<int> source)
                               {
                                   await foreach (var item in source)
                                   {
                                   }

                                   await foreach (var item in {|E128182:source|})
                                   {
                                   }

                                   await foreach (var item in {|E128182:source|})
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SingleEnumeration_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(IAsyncEnumerable<int> source)
                               {
                                   await foreach (var item in source)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InlineCreatedSequence_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               private static async IAsyncEnumerable<int> Build()
                               {
                                   yield return 1;
                                   await Task.Yield();
                               }

                               private async Task M()
                               {
                                   await foreach (var item in Build())
                                   {
                                   }

                                   await foreach (var item in Build())
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IteratorWithYieldReturn_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               private async IAsyncEnumerable<int> M(IAsyncEnumerable<int> source)
                               {
                                   await foreach (var item in source)
                                   {
                                       yield return item;
                                   }

                                   await foreach (var item in source)
                                   {
                                       yield return item;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EnumeratorCancellationParameter_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Runtime.CompilerServices;
                           using System.Threading;
                           using System.Threading.Tasks;

                           class C
                           {
                               private async IAsyncEnumerable<int> M(
                                   IAsyncEnumerable<int> source,
                                   [EnumeratorCancellation] CancellationToken cancellationToken)
                               {
                                   await foreach (var item in source.WithCancellation(cancellationToken))
                                   {
                                       yield return item;
                                   }

                                   await foreach (var item in source.WithCancellation(cancellationToken))
                                   {
                                       yield return item;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DifferentSequences_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(IAsyncEnumerable<int> first, IAsyncEnumerable<int> second)
                               {
                                   await foreach (var item in first)
                                   {
                                   }

                                   await foreach (var item in second)
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EnumeratedTwiceInsideOneLambda_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               void M(IAsyncEnumerable<int> source)
                               {
                                   Func<Task> run = async () =>
                                   {
                                       await foreach (var item in source)
                                       {
                                       }

                                       await foreach (var item in {|E128182:source|})
                                       {
                                       }
                                   };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EnumeratedInDifferentSwitchSections_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               async Task M(IAsyncEnumerable<int> source, int mode)
                               {
                                   switch (mode)
                                   {
                                       case 0:
                                           await foreach (var item in source)
                                           {
                                           }

                                           break;

                                       default:
                                           await foreach (var item in source)
                                           {
                                           }

                                           break;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EnumeratedTwiceInsideOneAccessor_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               private readonly IAsyncEnumerable<int> _source = Build();

                               private static async IAsyncEnumerable<int> Build()
                               {
                                   yield return 1;
                                   await Task.Yield();
                               }

                               public IAsyncEnumerator<int> Enumerator
                               {
                                   get
                                   {
                                       _source.GetAsyncEnumerator();
                                       return {|E128182:_source|}.GetAsyncEnumerator();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedLocalFunctionBody_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;

                           class C
                           {
                               private async Task M(IAsyncEnumerable<int> source)
                               {
                                   await foreach (var item in source)
                                   {
                                   }

                                   await Inner();

                                   async Task Inner()
                                   {
                                       await foreach (var item in source)
                                       {
                                       }
                                   }
                               }
                           }
                           """);
    }
}
