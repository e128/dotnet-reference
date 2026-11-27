using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PaddedStructMarshalCopyAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheExplicitStructCarriesPadding()
    {
        return AnalyzerTestRunner.VerifyAsync<PaddedStructMarshalCopyAnalyzer>("""
                           using System;
                           using System.Runtime.InteropServices;

                           [StructLayout(LayoutKind.Explicit, Size = 16)]
                           struct Padded
                           {
                               [FieldOffset(0)] public int Value;
                           }

                           class C
                           {
                               void Run()
                               {
                                   Span<Padded> span = default;
                                   var bytes = [|MemoryMarshal.AsBytes(span)|];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStructHasAGapBetweenFields()
    {
        return AnalyzerTestRunner.VerifyAsync<PaddedStructMarshalCopyAnalyzer>("""
                           using System;
                           using System.Runtime.InteropServices;

                           [StructLayout(LayoutKind.Explicit)]
                           struct Padded
                           {
                               [FieldOffset(0)] public int First;
                               [FieldOffset(8)] public int Second;
                           }

                           class C
                           {
                               void Run()
                               {
                                   Span<Padded> span = default;
                                   var bytes = [|MemoryMarshal.AsBytes(span)|];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheExplicitStructHasNoPadding()
    {
        return AnalyzerTestRunner.VerifyAsync<PaddedStructMarshalCopyAnalyzer>("""
                           using System;
                           using System.Runtime.InteropServices;

                           [StructLayout(LayoutKind.Explicit, Size = 4)]
                           struct Packed
                           {
                               [FieldOffset(0)] public int Value;
                           }

                           class C
                           {
                               void Run()
                               {
                                   Span<Packed> span = default;
                                   var bytes = MemoryMarshal.AsBytes(span);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStructHasNoExplicitLayout()
    {
        return AnalyzerTestRunner.VerifyAsync<PaddedStructMarshalCopyAnalyzer>("""
                           using System;
                           using System.Runtime.InteropServices;

                           struct Plain
                           {
                               public int Value;
                           }

                           class C
                           {
                               void Run()
                               {
                                   Span<Plain> span = default;
                                   var bytes = MemoryMarshal.AsBytes(span);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCallIsMemoryMarshalCast()
    {
        return AnalyzerTestRunner.VerifyAsync<PaddedStructMarshalCopyAnalyzer>("""
                           using System;
                           using System.Runtime.InteropServices;

                           [StructLayout(LayoutKind.Explicit, Size = 16)]
                           struct Padded
                           {
                               [FieldOffset(0)] public int Value;
                           }

                           class C
                           {
                               void Run()
                               {
                                   Span<Padded> span = default;
                                   var bytes = [|MemoryMarshal.Cast<Padded, byte>(span)|];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCallIsUnsafeAs()
    {
        return AnalyzerTestRunner.VerifyAsync<PaddedStructMarshalCopyAnalyzer>("""
                           using System;
                           using System.Runtime.CompilerServices;
                           using System.Runtime.InteropServices;

                           [StructLayout(LayoutKind.Explicit, Size = 16)]
                           struct Padded
                           {
                               [FieldOffset(0)] public int Value;
                           }

                           class C
                           {
                               void Run()
                               {
                                   var value = default(Padded);
                                   var bits = [|Unsafe.As<Padded, byte>(ref value)|];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCallIsBufferBlockCopy()
    {
        return AnalyzerTestRunner.VerifyAsync<PaddedStructMarshalCopyAnalyzer>("""
                           using System;
                           using System.Runtime.InteropServices;

                           [StructLayout(LayoutKind.Explicit, Size = 16)]
                           struct Padded
                           {
                               [FieldOffset(0)] public int Value;
                           }

                           class C
                           {
                               void Run()
                               {
                                   var source = new Padded[1];
                                   var destination = new byte[16];
                                   [|Buffer.BlockCopy(source, 0, destination, 0, 16)|];
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCallIsStructureToPtr()
    {
        return AnalyzerTestRunner.VerifyAsync<PaddedStructMarshalCopyAnalyzer>("""
                           using System;
                           using System.Runtime.InteropServices;

                           [StructLayout(LayoutKind.Explicit, Size = 16)]
                           struct Padded
                           {
                               [FieldOffset(0)] public int Value;
                           }

                           class C
                           {
                               void Run()
                               {
                                   var value = default(Padded);
                                   var pointer = IntPtr.Zero;
                                   [|Marshal.StructureToPtr(value, pointer, false)|];
                               }
                           }
                           """);
    }
}
