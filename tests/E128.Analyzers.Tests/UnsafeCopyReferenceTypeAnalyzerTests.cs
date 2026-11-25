using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeCopyReferenceTypeAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTypeHoldsAReference()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeCopyReferenceTypeAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(ref Holder target, void* source)
                               {
                                   {|E128129:Unsafe.Copy(ref target, source)|};
                               }
                           }

                           class Holder
                           {
                               public string Text { get; set; } = string.Empty;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsUnmanaged()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeCopyReferenceTypeAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(ref int target, void* source)
                               {
                                   Unsafe.Copy(ref target, source);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenUnsafeWriteStoresAReference()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeCopyReferenceTypeAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(void* destination, Holder value)
                               {
                                   {|E128129:Unsafe.Write(destination, value)|};
                                   {|E128129:Unsafe.WriteUnaligned(destination, value)|};
                               }
                           }

                           class Holder
                           {
                               public string Text { get; set; } = string.Empty;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenUnsafeWriteStoresAnInt()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeCopyReferenceTypeAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(void* destination)
                               {
                                   Unsafe.Write(destination, 1);
                                   Unsafe.WriteUnaligned(destination, 2);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCopyBlockMovesAPointerToAReference()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeCopyReferenceTypeAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(void* destination, Holder[] source)
                               {
                                   fixed (Holder* pointer = source)
                                   {
                                       {|E128129:Unsafe.CopyBlock(destination, pointer, 8u)|};
                                   }
                               }
                           }

                           class Holder
                           {
                               public string Text { get; set; } = string.Empty;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenCopyBlockMovesABytePointer()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeCopyReferenceTypeAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(void* destination, byte[] source)
                               {
                                   fixed (byte* pointer = source)
                                   {
                                       Unsafe.CopyBlock(destination, pointer, 8u);
                                   }
                               }
                           }
                           """);
    }
}
