using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ManualSpanSumAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<ManualSpanSumAnalyzer>("using System;\n\n" + TestStubs.TensorPrimitives + "\n\n" + code);
    }

    private static Task VerifyWithoutStubAsync(string code)
    {
        return new CSharpAnalyzerTest<ManualSpanSumAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALoopSumsAFloatArray()
    {
        return VerifyAsync("""
                           class C
                           {
                               float Sum(float[] values)
                               {
                                   float sum = 0;
                                   {|E128135:foreach (var value in values)
                                   {
                                       sum += value;
                                   }|}
                                   return sum;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALoopSumsADoubleSpan()
    {
        return VerifyAsync("""
                           class C
                           {
                               double Sum(ReadOnlySpan<double> values)
                               {
                                   double sum = 0;
                                   {|E128135:foreach (var value in values)
                                   {
                                       sum += value;
                                   }|}
                                   return sum;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTensorTypeIsMissing()
    {
        return VerifyWithoutStubAsync("""
                                      class C
                                      {
                                          float Sum(float[] values)
                                          {
                                              float sum = 0;
                                              foreach (var value in values)
                                              {
                                                  sum += value;
                                              }
                                              return sum;
                                          }
                                      }
                                      """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheElementTypeIsInt()
    {
        return VerifyAsync("""
                           class C
                           {
                               int Sum(int[] values)
                               {
                                   int sum = 0;
                                   foreach (var value in values)
                                   {
                                       sum += value;
                                   }
                                   return sum;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyDoesMoreThanAdd()
    {
        return VerifyAsync("""
                           class C
                           {
                               float Sum(float[] values)
                               {
                                   float sum = 0;
                                   foreach (var value in values)
                                   {
                                       sum += value;
                                       sum *= 2;
                                   }
                                   return sum;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSumIsADoubleAndTheSourceIsFloat()
    {
        return VerifyAsync("""
                           class C
                           {
                               double Sum(float[] values)
                               {
                                   double sum = 0;
                                   foreach (var value in values)
                                   {
                                       sum += value;
                                   }
                                   return sum;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALoopComputesTheMaximum()
    {
        return VerifyAsync("""
                           class C
                           {
                               double Max(double[] values)
                               {
                                   double max = double.MinValue;
                                   {|E128135:foreach (var value in values)
                                   {
                                       if (value > max)
                                       {
                                           max = value;
                                       }
                                   }|}
                                   return max;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALoopComputesTheMinimum()
    {
        return VerifyAsync("""
                           class C
                           {
                               float Min(float[] values)
                               {
                                   float min = float.MaxValue;
                                   {|E128135:foreach (var value in values)
                                   {
                                       if (value < min)
                                       {
                                           min = value;
                                       }
                                   }|}
                                   return min;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMaximumStartsAtAnArbitraryValue()
    {
        return VerifyAsync("""
                           class C
                           {
                               double Max(double[] values)
                               {
                                   double max = 0;
                                   foreach (var value in values)
                                   {
                                       if (value > max)
                                       {
                                           max = value;
                                       }
                                   }
                                   return max;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMinimumStartsAtZero()
    {
        return VerifyAsync("""
                           class C
                           {
                               double Min(double[] values)
                               {
                                   double min = 0;
                                   foreach (var value in values)
                                   {
                                       if (value < min)
                                       {
                                           min = value;
                                       }
                                   }
                                   return min;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAccumulatorIsWrittenBeforeTheLoop()
    {
        return VerifyAsync("""
                           class C
                           {
                               double Max(double[] values)
                               {
                                   double max = double.MinValue;
                                   max = 0;
                                   foreach (var value in values)
                                   {
                                       if (value > max)
                                       {
                                           max = value;
                                       }
                                   }
                                   return max;
                               }
                           }
                           """);
    }
}
