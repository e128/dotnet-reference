using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ManualSpanSumAnalyzerBranchCoverageGapTests
{
    private const string DoubleOnlyStub = """
        namespace System.Numerics.Tensors
        {
            public static class TensorPrimitives
            {
                public static double Sum(ReadOnlySpan<double> values)
                {
                    return 0d;
                }

                public static float Sum(ReadOnlySpan<float> values, int stride)
                {
                    return 0f;
                }

                public static int Sum(ReadOnlySpan<int> values)
                {
                    return 0;
                }
            }
        }
        """;

    private const string IntOnlyStub = """
        namespace System.Numerics.Tensors
        {
            public static class TensorPrimitives
            {
                public static int Sum(ReadOnlySpan<int> values)
                {
                    return 0;
                }
            }
        }
        """;

    private static Task VerifyAsync(string code)
    {
        return VerifyWithStubAsync(TestStubs.TensorPrimitives, code);
    }

    private static Task VerifyWithStubAsync(string stub, string code)
    {
        return AnalyzerTestRunner.VerifyAsync<ManualSpanSumAnalyzer>("using System;\n\n" + stub + "\n\n" + code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALoopSumsAFloatSpan()
    {
        return VerifyAsync("""
                           class C
                           {
                               float Sum(Span<float> values)
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
    public Task Silent_WhenTheSourceIsNotAnArrayOrSpan()
    {
        return VerifyAsync("""
                           class C
                           {
                               float FromList(System.Collections.Generic.List<float> values)
                               {
                                   float sum = 0;
                                   foreach (var value in values)
                                   {
                                       sum += value;
                                   }
                                   return sum;
                               }

                               float FromSequence(System.Collections.Generic.IEnumerable<float> values)
                               {
                                   float sum = 0;
                                   foreach (var value in values)
                                   {
                                       sum += value;
                                   }
                                   return sum;
                               }

                               float FromText(string text)
                               {
                                   float sum = 0;
                                   foreach (var letter in text)
                                   {
                                       sum += letter;
                                   }
                                   return sum;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnlyForTheTypesThatTensorPrimitivesSums()
    {
        return VerifyWithStubAsync(
            DoubleOnlyStub,
            """
            class C
            {
                double SumDouble(double[] values)
                {
                    double sum = 0;
                    {|E128135:foreach (var value in values)
                    {
                        sum += value;
                    }|}
                    return sum;
                }

                float SumFloat(float[] values)
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
    public Task Silent_WhenTensorPrimitivesHasNoMax()
    {
        return VerifyWithStubAsync(
            DoubleOnlyStub,
            """
            class C
            {
                double Max(double[] values)
                {
                    double max = double.MinValue;
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
    public Task Silent_WhenTensorPrimitivesSumsNeitherFloatNorDouble()
    {
        return VerifyWithStubAsync(
            IntOnlyStub,
            """
            class C
            {
                double Sum(double[] values)
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
