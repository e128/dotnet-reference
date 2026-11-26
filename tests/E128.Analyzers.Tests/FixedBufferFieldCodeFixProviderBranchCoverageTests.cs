using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FixedBufferFieldCodeFixProviderBranchCoverageTests
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
    public Task Fix_UsesDefaultAccessibilityWhenTheFieldHasNoAccessibilityModifier()
    {
        return VerifyFixAsync(
            """
            unsafe struct Buffer
            {
                {|E128119:fixed byte Data[4];|}
            }
            """,
            """
            unsafe struct Buffer
            {
                DataBuffer Data;

                [System.Runtime.CompilerServices.InlineArray(4)]
                struct DataBuffer
                {
                    private byte _element0;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsInternalAccessibilityForTheGeneratedBuffer()
    {
        return VerifyFixAsync(
            """
            unsafe struct Buffer
            {
                {|E128119:internal fixed int Data[8];|}
            }
            """,
            """
            unsafe struct Buffer
            {
                internal DataBuffer Data;

                [System.Runtime.CompilerServices.InlineArray(8)]
                internal struct DataBuffer
                {
                    private int _element0;
                }
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("private struct DataBuffer { }")]
    [InlineData("private int DataBuffer;")]
    [InlineData("private void DataBuffer() { }")]
    public Task NoFix_WhenTheGeneratedTypeNameConflictsWithAMember(string member)
    {
        var code = "unsafe struct Buffer { {|E128119:public fixed byte Data[4];|} " + member + " }";
        return VerifyFixAsync(code, code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_IgnoresUnrelatedPropertiesMethodsAndFields()
    {
        return VerifyFixAsync(
            """
            unsafe struct Buffer
            {
                private int Other;
                private int Value => 1;
                private void M() { }
                {|E128119:public fixed byte Data[4];|}
            }
            """,
            """
            unsafe struct Buffer
            {
                private int Other;
                private int Value => 1;
                private void M() { }
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
