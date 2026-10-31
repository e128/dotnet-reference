using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ManualSpanSumCodeFixTests
{
    private const string TensorStub = """
                                      namespace System.Numerics.Tensors
                                      {
                                          public static class TensorPrimitives
                                          {
                                              public static float Sum(ReadOnlySpan<float> values)
                                              {
                                                  return 0f;
                                              }

                                              public static double Sum(ReadOnlySpan<double> values)
                                              {
                                                  return 0d;
                                              }
                                          }
                                      }
                                      """;

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<
            ManualSpanSumAnalyzer,
            ManualSpanSumCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = "using System;\n\n" + TensorStub + "\n\n" + source,
            FixedCode = "using System;\nusing System.Numerics.Tensors;\n\n" + TensorStub + "\n\n" + fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheLoopWithSum()
    {
        return VerifyFixAsync(
            """
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
            """,
            """
            class C
            {
                float Sum(float[] values)
                {
                    float sum = 0;
                    sum += TensorPrimitives.Sum(values);
                    return sum;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheWrittenSumOnTheLeft()
    {
        return VerifyFixAsync(
            """
            class C
            {
                double Sum(ReadOnlySpan<double> values)
                {
                    double total = 0;
                    {|E128135:foreach (var value in values)
                    {
                        total += value;
                    }|}
                    return total;
                }
            }
            """,
            """
            class C
            {
                double Sum(ReadOnlySpan<double> values)
                {
                    double total = 0;
                    total += TensorPrimitives.Sum(values);
                    return total;
                }
            }
            """);
    }
}
