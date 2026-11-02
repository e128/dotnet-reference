using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FixedBufferFieldCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        var test = new CSharpCodeFixTest<
            FixedBufferFieldAnalyzer,
            FixedBufferFieldCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.SolutionTransforms.Add(CodeFixTestRunner.AllowUnsafe);

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheBufferWithAnInlineArray()
    {
        return VerifyFixAsync(
            """
            unsafe struct Buffer
            {
                {|E128119:public fixed byte Data[16];|}
            }
            """,
            """
            unsafe struct Buffer
            {
                public DataBuffer Data;

                [System.Runtime.CompilerServices.InlineArray(16)]
                public struct DataBuffer
                {
                    private byte _element0;
                }
            }
            """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_HandlesNestedTypesAndSeveralBuffers()
    {
        return VerifyFixAsync(
            """
            class Outer
            {
                public unsafe struct Inner
                {
                    {|E128119:public fixed byte First[4];|}
                    {|E128119:internal fixed int Second[8];|}
                }
            }
            """,
            """
            class Outer
            {
                public unsafe struct Inner
                {
                    public FirstBuffer First;

                    [System.Runtime.CompilerServices.InlineArray(4)]
                    public struct FirstBuffer
                    {
                        private byte _element0;
                    }
                    internal SecondBuffer Second;

                    [System.Runtime.CompilerServices.InlineArray(8)]
                    internal struct SecondBuffer
                    {
                        private int _element0;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheFieldIsUsed()
    {
        const string Code = """
            unsafe struct Buffer
            {
                {|E128119:public fixed byte Data[16];|}

                public byte* Start()
                {
                    fixed (byte* pointer = Data)
                    {
                        return pointer;
                    }
                }
            }
            """;

        return VerifyFixAsync(Code, Code);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenOneDeclarationHoldsSeveralBuffers()
    {
        const string Code = """
            unsafe struct S
            {
                {|E128119:public fixed byte A[4], B[8];|}
            }
            """;

        return VerifyFixAsync(Code, Code);
    }
}
