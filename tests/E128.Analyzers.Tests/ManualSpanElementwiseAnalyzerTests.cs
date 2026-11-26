using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ManualSpanElementwiseAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<ManualSpanElementwiseAnalyzer>(
            "using System;\n\n" + TestStubs.TensorPrimitives + "\n\n" + code);
    }

    private static Task VerifyWithoutStubAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<ManualSpanElementwiseAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALoopAddsTwoFloatArrays()
    {
        return VerifyAsync("""
                           class C
                           {
                               void Add(float[] a, float[] b, float[] destination)
                               {
                                   {|E128255:for (var i = 0; i < a.Length; i++)
                                   {
                                       destination[i] = a[i] + b[i];
                                   }|}
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALoopMultipliesTwoDoubleSpans()
    {
        return VerifyAsync("""
                           class C
                           {
                               void Multiply(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> destination)
                               {
                                   {|E128255:for (var i = 0; i < b.Length; i++)
                                   {
                                       destination[i] = a[i] * b[i];
                                   }|}
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
                                          void Add(float[] a, float[] b, float[] destination)
                                          {
                                              for (var i = 0; i < a.Length; i++)
                                              {
                                                  destination[i] = a[i] + b[i];
                                              }
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
                               void Add(int[] a, int[] b, int[] destination)
                               {
                                   for (var i = 0; i < a.Length; i++)
                                   {
                                       destination[i] = a[i] + b[i];
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBoundIsTheDestinationLength()
    {
        return VerifyAsync("""
                           class C
                           {
                               void Add(float[] a, float[] b, float[] destination)
                               {
                                   for (var i = 0; i < destination.Length; i++)
                                   {
                                       destination[i] = a[i] + b[i];
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBodyHasASecondStatement()
    {
        return VerifyAsync("""
                           class C
                           {
                               void Add(float[] a, float[] b, float[] destination)
                               {
                                   var count = 0;
                                   for (var i = 0; i < a.Length; i++)
                                   {
                                       destination[i] = a[i] + b[i];
                                       count++;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSourceIndexIsOffset()
    {
        return VerifyAsync("""
                           class C
                           {
                               void Add(float[] a, float[] b, float[] destination)
                               {
                                   for (var i = 0; i < a.Length; i++)
                                   {
                                       destination[i] = a[i + 1] + b[i];
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRightOperandIsAConstant()
    {
        return VerifyAsync("""
                           class C
                           {
                               void Add(float[] a, float[] destination)
                               {
                                   for (var i = 0; i < a.Length; i++)
                                   {
                                       destination[i] = a[i] + 1f;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheDestinationIsAList()
    {
        return VerifyAsync("""
                           class C
                           {
                               void Add(float[] a, float[] b, System.Collections.Generic.List<float> destination)
                               {
                                   for (var i = 0; i < a.Length; i++)
                                   {
                                       destination[i] = a[i] + b[i];
                                   }
                               }
                           }
                           """);
    }
}
