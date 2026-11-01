using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeReadWriteAlignmentAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        var test = new CSharpAnalyzerTest<UnsafeReadWriteAlignmentAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.SolutionTransforms.Add(CodeFixTestRunner.AllowUnsafe);

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenReadingThroughAPointer()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               int M(void* pointer)
                               {
                                   return {|E128113:Unsafe.Read<int>(pointer)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenWritingThroughAPointer()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(void* pointer)
                               {
                                   {|E128113:Unsafe.Write<int>(pointer, 1)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheUnalignedVariantIsUsed()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               int M(void* pointer)
                               {
                                   return Unsafe.ReadUnaligned<int>(pointer);
                               }
                           }
                           """);
    }
}
