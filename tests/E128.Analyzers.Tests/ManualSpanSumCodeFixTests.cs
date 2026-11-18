using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ManualSpanSumCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<ManualSpanSumAnalyzer, ManualSpanSumCodeFixProvider>(
            "using System;\n\n" + TestStubs.TensorPrimitives + "\n\n" + source,
            "using System;\nusing System.Numerics.Tensors;\n\n" + TestStubs.TensorPrimitives + "\n\n" + fixedCode);
    }

    private static Task VerifyFixWithStubLastAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<ManualSpanSumAnalyzer, ManualSpanSumCodeFixProvider>(
            "using System;\n\n" + source + "\n\n" + TestStubs.TensorPrimitives,
            "using System;\nusing System.Numerics.Tensors;\n\n" + fixedCode + "\n\n" + TestStubs.TensorPrimitives);
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

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheMaximumLoopWithMax()
    {
        return VerifyFixWithStubLastAsync(
            """
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
            """,
            """
            class C
            {
                double Max(double[] values)
                {
                    double max = TensorPrimitives.Max(values);
                    return max;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheMinimumLoopWithMin()
    {
        return VerifyFixWithStubLastAsync(
            """
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
            """,
            """
            class C
            {
                float Min(float[] values)
                {
                    float min = TensorPrimitives.Min(values);
                    return min;
                }
            }
            """);
    }
}
