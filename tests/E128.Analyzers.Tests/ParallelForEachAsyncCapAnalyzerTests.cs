using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ParallelForEachAsyncCapAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return VerifyWithReferencesAsync(ReferenceAssemblies.Net.Net100, code, expected);
    }

    private static Task VerifyWithReferencesAsync(
        ReferenceAssemblies references,
        string code,
        params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ParallelForEachAsyncCapAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = references
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoOptionsArgument_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task M()
                               {
                                   return {|E128187:Parallel.ForEachAsync(new List<int>(), (item, token) => default)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EmptyObjectCreation_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task M()
                               {
                                   return {|E128187:Parallel.ForEachAsync(new List<int>(), new ParallelOptions(), (item, token) => default)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InlineInitializerWithoutMaxDegreeOfParallelism_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task M()
                               {
                                   return {|E128187:Parallel.ForEachAsync(new List<int>(), new ParallelOptions { CancellationToken = CancellationToken.None }, (item, token) => default)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DefaultOptionsArgument_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task M()
                               {
                                   return {|E128187:Parallel.ForEachAsync(new List<int>(), default(ParallelOptions), (item, token) => default)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CappedInitializer_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task M()
                               {
                                   var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
                                   return Parallel.ForEachAsync(new List<int>(), options, (item, token) => default);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CappedToMinusOne_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task M()
                               {
                                   var options = new ParallelOptions { MaxDegreeOfParallelism = -1 };
                                   return Parallel.ForEachAsync(new List<int>(), options, (item, token) => default);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CappedToExpression_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task M(int maxDegree)
                               {
                                   var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegree };
                                   return Parallel.ForEachAsync(new List<int>(), options, (item, token) => default);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task VariableOptionsArgument_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           class C
                           {
                               Task M(ParallelOptions options)
                               {
                                   return Parallel.ForEachAsync(new List<int>(), options, (item, token) => default);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FactoryCallOptionsArgument_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Threading.Tasks;
                           static class ParallelOptionsFactory
                           {
                               public static ParallelOptions Create()
                               {
                                   return new ParallelOptions();
                               }
                           }
                           class C
                           {
                               Task M()
                               {
                                   var options = ParallelOptionsFactory.Create();
                                   return Parallel.ForEachAsync(new List<int>(), options, (item, token) => default);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OtherTypeWithForEachAsync_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           static class Parallel
                           {
                               public static void ForEachAsync<T>(IEnumerable<T> source)
                               {
                               }
                           }
                           class C
                           {
                               void M()
                               {
                                   Parallel.ForEachAsync(new List<int>());
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParallelFor_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Threading.Tasks;
                           class C
                           {
                               void M()
                               {
                                   Parallel.For(0, 10, i => { });
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CompilationWithoutParallelType_DoesNotFire()
    {
        return VerifyWithReferencesAsync(
            ReferenceAssemblies.NetStandard.NetStandard10,
            """
            using System.Collections.Generic;
            static class Parallel
            {
                public static void ForEachAsync<T>(IEnumerable<T> source)
                {
                }
            }
            class C
            {
                void M()
                {
                    Parallel.ForEachAsync(new List<int>());
                }
            }
            """);
    }
}
