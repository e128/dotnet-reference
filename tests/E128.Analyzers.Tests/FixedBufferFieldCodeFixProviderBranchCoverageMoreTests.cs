using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FixedBufferFieldCodeFixProviderBranchCoverageMoreTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        var test = new CSharpCodeFixTest<FixedBufferFieldAnalyzer, FixedBufferFieldCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.SolutionTransforms.Add(CodeFixTestRunner.AllowUnsafe);
        return test.RunAsync(TestContext.Current.CancellationToken);
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
