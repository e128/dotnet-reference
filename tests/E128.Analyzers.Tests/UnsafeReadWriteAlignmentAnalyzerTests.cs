using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeReadWriteAlignmentAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { AllowUnsafe = true };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenReadingThroughAPointer()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeReadWriteAlignmentAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               int M(void* pointer)
                               {
                                   return {|E128113:Unsafe.Read<int>(pointer)|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenWritingThroughAPointer()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeReadWriteAlignmentAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(void* pointer)
                               {
                                   {|E128113:Unsafe.Write<int>(pointer, 1)|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheUnalignedVariantIsUsed()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeReadWriteAlignmentAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               int M(void* pointer)
                               {
                                   return Unsafe.ReadUnaligned<int>(pointer);
                               }
                           }
                           """, options: Options);
    }
}
