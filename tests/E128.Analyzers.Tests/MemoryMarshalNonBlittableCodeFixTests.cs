using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MemoryMarshalNonBlittableCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<
            MemoryMarshalNonBlittableAnalyzer,
            MemoryMarshalNonBlittableCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesBoolReadWithAByteComparison()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Runtime.InteropServices;
            class C
            {
                bool M(ReadOnlySpan<byte> span)
                {
                    return {|E128118:MemoryMarshal.Read<bool>(span)|};
                }
            }
            """,
            """
            using System;
            using System.Runtime.InteropServices;
            class C
            {
                bool M(ReadOnlySpan<byte> span)
                {
                    return (span[0] != 0);
                }
            }
            """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ParenthesizesACastSource()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Runtime.InteropServices;
            class C
            {
                bool M(byte[] data)
                {
                    return {|E128118:MemoryMarshal.Read<bool>((ReadOnlySpan<byte>)data)|};
                }
            }
            """,
            """
            using System;
            using System.Runtime.InteropServices;
            class C
            {
                bool M(byte[] data)
                {
                    return (((ReadOnlySpan<byte>)data)[0] != 0);
                }
            }
            """);
    }
}
