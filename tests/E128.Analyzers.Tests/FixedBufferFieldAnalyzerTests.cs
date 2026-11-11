using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FixedBufferFieldAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { AllowUnsafe = true };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAFixedSizeBufferIsDeclared()
    {
        return AnalyzerTestRunner.VerifyAsync<FixedBufferFieldAnalyzer>("""
                           unsafe struct Buffer
                           {
                               {|E128119:public fixed byte Data[16];|}
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFieldIsNotFixed()
    {
        return AnalyzerTestRunner.VerifyAsync<FixedBufferFieldAnalyzer>("""
                           unsafe struct Buffer
                           {
                               public int Data;
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOneDeclarationHoldsSeveralBuffers()
    {
        return AnalyzerTestRunner.VerifyAsync<FixedBufferFieldAnalyzer>("""
                           unsafe struct S
                           {
                               {|E128119:public fixed byte A[4], B[8];|}
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSizeIsAConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<FixedBufferFieldAnalyzer>("""
                           unsafe struct S
                           {
                               private const int Size = 16;

                               {|E128119:public fixed byte Data[Size];|}
                           }
                           """, options: Options);
    }
}
