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
        return new CSharpAnalyzerTest<ManualSpanSumAnalyzer, DefaultVerifier>
        {
            TestCode = "using System;\n\n" + TestStubs.TensorPrimitives + "\n\n" + code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    private static Task VerifyWithoutStubAsync(string code)
    {
        return new CSharpAnalyzerTest<ManualSpanSumAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
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
}
