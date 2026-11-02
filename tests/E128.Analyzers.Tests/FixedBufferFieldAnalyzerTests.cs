using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FixedBufferFieldAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        var test = new CSharpAnalyzerTest<FixedBufferFieldAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.SolutionTransforms.Add(CodeFixTestRunner.AllowUnsafe);

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAFixedSizeBufferIsDeclared()
    {
        return VerifyAsync("""
                           unsafe struct Buffer
                           {
                               {|E128119:public fixed byte Data[16];|}
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFieldIsNotFixed()
    {
        return VerifyAsync("""
                           unsafe struct Buffer
                           {
                               public int Data;
                           }
                           """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOneDeclarationHoldsSeveralBuffers()
    {
        return VerifyAsync("""
                           unsafe struct S
                           {
                               {|E128119:public fixed byte A[4], B[8];|}
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSizeIsAConstant()
    {
        return VerifyAsync("""
                           unsafe struct S
                           {
                               private const int Size = 16;

                               {|E128119:public fixed byte Data[Size];|}
                           }
                           """);
    }
}
