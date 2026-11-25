using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ManualSpanElementwiseCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<ManualSpanElementwiseAnalyzer, ManualSpanElementwiseCodeFixProvider>(
            "using System;\n\n" + TestStubs.TensorPrimitives + "\n\n" + source,
            "using System;\nusing System.Numerics.Tensors;\n\n" + TestStubs.TensorPrimitives + "\n\n" + fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheAddLoopWithTensorPrimitives()
    {
        return VerifyFixAsync(
            """
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
            """,
            """
            class C
            {
                void Add(float[] a, float[] b, float[] destination)
                {
                    TensorPrimitives.Add(a, b, destination);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheBoundOwnerAsTheLengthDriver()
    {
        return VerifyFixAsync(
            """
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
            """,
            """
            class C
            {
                void Multiply(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> destination)
                {
                    TensorPrimitives.Multiply(b, a, destination);
                }
            }
            """);
    }
}
