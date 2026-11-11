using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MemoryMarshalNonBlittableAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenReadingABool()
    {
        return AnalyzerTestRunner.VerifyAsync<MemoryMarshalNonBlittableAnalyzer>("""
                           using System;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               bool M(ReadOnlySpan<byte> span)
                               {
                                   return {|E128118:MemoryMarshal.Read<bool>(span)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCastingToDecimal()
    {
        return AnalyzerTestRunner.VerifyAsync<MemoryMarshalNonBlittableAnalyzer>("""
                           using System;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               decimal M(ReadOnlySpan<byte> span)
                               {
                                   return {|E128118:MemoryMarshal.Cast<byte, decimal>(span)|}[0];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenReadingAnInt()
    {
        return AnalyzerTestRunner.VerifyAsync<MemoryMarshalNonBlittableAnalyzer>("""
                           using System;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               int M(ReadOnlySpan<byte> span)
                               {
                                   return MemoryMarshal.Read<int>(span);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAStructHoldsABool()
    {
        return AnalyzerTestRunner.VerifyAsync<MemoryMarshalNonBlittableAnalyzer>("""
                           using System;
                           using System.Runtime.InteropServices;
                           struct Header
                           {
                               public int Length;
                               public bool Flag;
                           }
                           class C
                           {
                               Header M(ReadOnlySpan<byte> data)
                               {
                                   return {|E128118:MemoryMarshal.Read<Header>(data)|};
                               }
                           }
                           """);
    }
}
