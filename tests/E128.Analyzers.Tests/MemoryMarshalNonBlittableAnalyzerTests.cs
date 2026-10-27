using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MemoryMarshalNonBlittableAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<MemoryMarshalNonBlittableAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenReadingABool()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
        return VerifyAsync("""
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
