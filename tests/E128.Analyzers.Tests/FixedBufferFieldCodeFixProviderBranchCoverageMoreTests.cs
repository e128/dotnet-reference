using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FixedBufferFieldCodeFixProviderBranchCoverageMoreTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<FixedBufferFieldAnalyzer, FixedBufferFieldCodeFixProvider>(
            source,
            fixedCode,
            options: new CodeFixTestOptions { AllowUnsafe = true });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_IgnoresMembersThatCannotShareTheBufferName()
    {
        return VerifyFixAsync(
            """
            unsafe struct Buffer
            {
                private delegate void Callback();

                public Buffer(int value)
                {
                }

                {|E128119:public fixed byte Data[4];|}
            }
            """,
            """
            unsafe struct Buffer
            {
                private delegate void Callback();

                public Buffer(int value)
                {
                }

                public DataBuffer Data;

                [System.Runtime.CompilerServices.InlineArray(4)]
                public struct DataBuffer
                {
                    private byte _element0;
                }
            }
            """);
    }
}
