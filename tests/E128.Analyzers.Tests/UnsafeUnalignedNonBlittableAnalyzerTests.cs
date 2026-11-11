using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeUnalignedNonBlittableAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { AllowUnsafe = true };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenReadingABool()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeUnalignedNonBlittableAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               bool M(ref byte source)
                               {
                                   return {|E128139:Unsafe.ReadUnaligned<bool>(ref source)|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenReadingABoolFromAnInArgument()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeUnalignedNonBlittableAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               bool M(ref readonly byte source)
                               {
                                   return {|E128139:Unsafe.ReadUnaligned<bool>(in source)|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenWritingABool()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeUnalignedNonBlittableAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(ref byte destination, bool value)
                               {
                                   {|E128139:Unsafe.WriteUnaligned<bool>(ref destination, value)|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStructHoldsABool()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeUnalignedNonBlittableAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           struct Flag
                           {
                               public bool Value;
                           }
                           unsafe class C
                           {
                               Flag M(ref byte source)
                               {
                                   return {|E128139:Unsafe.ReadUnaligned<Flag>(ref source)|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsBlittable()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeUnalignedNonBlittableAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               int M(ref byte source)
                               {
                                   return Unsafe.ReadUnaligned<int>(ref source);
                               }
                           }
                           """, options: Options);
    }
}
