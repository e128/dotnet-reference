using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ComplexityCeilingE128AnalyzerTests
{
    private const string NestedLoopCode = """
                                          class C
                                          {
                                              void M(string[] items)
                                              {
                                                  foreach (var a in items)
                                                  {
                                                      foreach (var b in items)
                                                      {
                                                          _ = a.Length + b.Length;
                                                      }
                                                  }
                                              }
                                          }
                                          """;

    private const string SortInLoopCode = """
                                          using System.Collections.Generic;
                                          class C
                                          {
                                              void M(List<int> items)
                                              {
                                                  foreach (var item in items)
                                                  {
                                                      items.Sort();
                                                  }
                                              }
                                          }
                                          """;

    private const string SuppressedNestedLoopCode = """
                                                    #pragma warning disable E128152
                                                    class C
                                                    {
                                                        void M(string[] items)
                                                        {
                                                            foreach (var a in items)
                                                            {
                                                                foreach (var b in items)
                                                                {
                                                                    _ = a.Length + b.Length;
                                                                }
                                                            }
                                                        }
                                                    }
                                                    """;

    private const string DepthThreeLoopCode = """
                                              class C
                                              {
                                                  void M(string[] items)
                                                  {
                                                      foreach (var a in items)
                                                      {
                                                          foreach (var b in items)
                                                          {
                                                              foreach (var c in items)
                                                              {
                                                                  _ = a.Length + b.Length + c.Length;
                                                              }
                                                          }
                                                      }
                                                  }
                                              }
                                              """;

    private const string LoopInsideLambdaLoopCode = """
                                                    class C
                                                    {
                                                        void M(string[] items)
                                                        {
                                                            foreach (var a in items)
                                                            {
                                                                System.Action act = () =>
                                                                {
                                                                    foreach (var b in items)
                                                                    {
                                                                        _ = a.Length + b.Length;
                                                                    }
                                                                };
                                                            }
                                                        }
                                                    }
                                                    """;

    private const string LoopInsideLocalFunctionLoopCode = """
                                                           class C
                                                           {
                                                               void M(string[] items)
                                                               {
                                                                   foreach (var a in items)
                                                                   {
                                                                       int Probe(int v)
                                                                       {
                                                                           for (var i = 0; i < v; i++)
                                                                           {
                                                                               _ = a.Length + i;
                                                                           }
                                                                           return v;
                                                                       }
                                                                       _ = Probe(items.Length);
                                                                   }
                                                               }
                                                           }
                                                           """;

    private const string BranchingRecursionCode = """
                                                  class C
                                                  {
                                                      int Fib(int n)
                                                      {
                                                          if (n < 2)
                                                          {
                                                              return n;
                                                          }
                                                          return Fib(n - 1) + Fib(n - 2);
                                                      }
                                                  }
                                                  """;

    private const string LocalDictionaryMemoizedRecursionCode = """
                                                                using System.Collections.Generic;
                                                                class C
                                                                {
                                                                    int Fib(int n)
                                                                    {
                                                                        var memo = new Dictionary<int, int>();
                                                                        if (memo.TryGetValue(n, out var value))
                                                                        {
                                                                            return value;
                                                                        }
                                                                        memo[n] = Fib(n - 1) + Fib(n - 2);
                                                                        return memo[n];
                                                                    }
                                                                }
                                                                """;

    private const string MemoizedRecursionCode = """
                                                 class C
                                                 {
                                                     int[] cache = new int[64];

                                                     int Fib(int n)
                                                     {
                                                         if (n < 2)
                                                         {
                                                             return n;
                                                         }
                                                         cache[n] = Fib(n - 1) + Fib(n - 2);
                                                         return cache[n];
                                                     }
                                                 }
                                                 """;

    private const string ConstantInnerLoopCode = """
                                                 class C
                                                 {
                                                     void M(string[] items)
                                                     {
                                                         foreach (var a in items)
                                                         {
                                                             for (var i = 0; i < 4; i++)
                                                             {
                                                                 _ = a.Length + i;
                                                             }
                                                         }
                                                     }
                                                 }
                                                 """;

    private const string ConstantCollectionLoopCode = """
                                                      class C
                                                      {
                                                          void M()
                                                          {
                                                              foreach (var a in new[] { 1, 2, 3 })
                                                              {
                                                                  foreach (var b in new[] { 4, 5 })
                                                                  {
                                                                      _ = a + b;
                                                                  }
                                                              }
                                                          }
                                                      }
                                                      """;

    private const string DivisiveRecursionCode = """
                                                 class C
                                                 {
                                                     int Count(int n)
                                                     {
                                                         return n < 2 ? n : Count(n / 2) + Count(n / 2);
                                                     }
                                                 }
                                                 """;

    private const string WriteOnlyCacheRecursionCode = """
                                                       class C
                                                       {
                                                           int[] cache = new int[64];

                                                           int Fib(int n)
                                                           {
                                                               if (n < 2)
                                                               {
                                                                   return n;
                                                               }
                                                               cache[n] = Fib(n - 1) + Fib(n - 2);
                                                               return 0;
                                                           }
                                                       }
                                                       """;

    private const string CoordinateLoopCode = """
                                              class C
                                              {
                                                  void M(string[][] rows)
                                                  {
                                                      foreach (var row in rows)
                                                      {
                                                          foreach (var cell in row)
                                                          {
                                                              _ = cell;
                                                          }
                                                      }
                                                  }
                                              }
                                              """;

    private const string IndexedCoordinateLoopCode = """
                                                     class C
                                                     {
                                                         void M(string[][] rows)
                                                         {
                                                             foreach (var row in rows)
                                                             {
                                                                 for (var i = 0; i < row.Length; i++)
                                                                 {
                                                                     _ = row[i];
                                                                 }
                                                             }
                                                         }
                                                     }
                                                     """;

    private const string TriangularLoopCode = """
                                              class C
                                              {
                                                  void M(string[] items)
                                                  {
                                                      for (var i = 0; i < items.Length; i++)
                                                      {
                                                          for (var j = 0; j < i; j++)
                                                          {
                                                              _ = items[i].Length + items[j].Length;
                                                          }
                                                      }
                                                  }
                                              }
                                              """;

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ComplexityCeilingAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private static Task VerifyWithCeilingAsync(string ceiling, string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ComplexityCeilingAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", "is_global = true\ne128_max_complexity = " + ceiling));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_FlagsNestedLoop_WhenDepthExceedsCeiling()
    {
        return VerifyAsync(NestedLoopCode,
            new DiagnosticResult("E128152", DiagnosticSeverity.Warning)
                .WithLocation(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_Silent_WhenExistingRuleCoversShape()
    {
        return VerifyAsync(SortInLoopCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_UsesCeilingOption_WhenEditorconfigSetsTier()
    {
        return VerifyWithCeilingAsync("quadratic", NestedLoopCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_FlagsBranchingRecursion_WhenNoMemoization()
    {
        return VerifyAsync(BranchingRecursionCode,
            new DiagnosticResult("E128152", DiagnosticSeverity.Warning)
                .WithLocation(3, 5));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_Silent_WhenRecursionMemoizes()
    {
        return VerifyAsync(MemoizedRecursionCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_FallsBackToDefault_WhenOptionMalformed()
    {
        return VerifyWithCeilingAsync("not-a-tier", NestedLoopCode,
            new DiagnosticResult("E128152", DiagnosticSeverity.Warning)
                .WithLocation(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_Silent_WhenPragmaDisablesDiagnostic()
    {
        return VerifyAsync(SuppressedNestedLoopCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_ReportsDepthThreeNesting_AtMiddleAndInnerLoops()
    {
        return VerifyAsync(DepthThreeLoopCode,
            new DiagnosticResult("E128152", DiagnosticSeverity.Warning)
                .WithLocation(7, 13),
            new DiagnosticResult("E128152", DiagnosticSeverity.Warning)
                .WithLocation(9, 17));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_FiresAtLinearithmicCeiling_WhenDepthTwoLoopsPresent()
    {
        return VerifyWithCeilingAsync("linearithmic", NestedLoopCode,
            new DiagnosticResult("E128152", DiagnosticSeverity.Warning)
                .WithLocation(7, 13));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_QuadraticCeiling_StillFlagsDepthThreeLoop()
    {
        return VerifyWithCeilingAsync("quadratic", DepthThreeLoopCode,
            new DiagnosticResult("E128152", DiagnosticSeverity.Warning)
                .WithLocation(9, 17));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_Silent_WhenLoopLivesOnlyInsideLambda()
    {
        return VerifyAsync(LoopInsideLambdaLoopCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_Silent_WhenLoopLivesOnlyInsideLocalFunction()
    {
        return VerifyAsync(LoopInsideLocalFunctionLoopCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_Silent_WhenRecursionMemoizesIntoLocalDictionary()
    {
        return VerifyAsync(LocalDictionaryMemoizedRecursionCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_Silent_WhenInnerLoopHasConstantBound()
    {
        return VerifyAsync(ConstantInnerLoopCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_Silent_WhenLoopsRunOverConstantCollections()
    {
        return VerifyAsync(ConstantCollectionLoopCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_Silent_WhenRecursionHalvesTheArgument()
    {
        return VerifyAsync(DivisiveRecursionCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_FlagsBranchingRecursion_WhenCacheIsWrittenAndNeverRead()
    {
        return VerifyAsync(WriteOnlyCacheRecursionCode,
            new DiagnosticResult("E128152", DiagnosticSeverity.Warning)
                .WithLocation(5, 5));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_Silent_WhenCubicLoopMatchesCubicCeiling()
    {
        return VerifyWithCeilingAsync("cubic", DepthThreeLoopCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_Silent_WhenInnerLoopRunsOverOuterElement()
    {
        return VerifyAsync(CoordinateLoopCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_Silent_WhenIndexedLoopBoundIsOuterElement()
    {
        return VerifyAsync(IndexedCoordinateLoopCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplexityCeilingAnalyzer_FlagsNestedLoop_WhenInnerBoundReadsOuterCounter()
    {
        return VerifyAsync(TriangularLoopCode,
            new DiagnosticResult("E128152", DiagnosticSeverity.Warning)
                .WithLocation(7, 13));
    }
}
