using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ElementLoopToBulkSpanCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<
            ElementLoopToBulkSpanAnalyzer,
            ElementLoopToBulkSpanCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_UsesFillOnAnArray()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M(int[] values)
                {
                    {|E128140:for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = 1;
                    }|}
                }
            }
            """,
            """
            using System;

            class C
            {
                void M(int[] values)
                {
                    values.AsSpan().Fill(1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_UsesClearOnASpan()
    {
        return VerifyFixAsync(
            """
            using System;
            class C
            {
                void M(Span<byte> buffer)
                {
                    {|E128140:for (var i = 0; i < buffer.Length; i++)
                    {
                        buffer[i] = 0;
                    }|}
                }
            }
            """,
            """
            using System;
            class C
            {
                void M(Span<byte> buffer)
                {
                    buffer.Clear();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_UsesCopyTo()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M(byte[] source, byte[] destination)
                {
                    {|E128140:for (var i = 0; i < source.Length; i++)
                    {
                        destination[i] = source[i];
                    }|}
                }
            }
            """,
            """
            using System;

            class C
            {
                void M(byte[] source, byte[] destination)
                {
                    source.AsSpan().CopyTo(destination);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_UsesFillForANullableElement()
    {
        // Clear writes default(int?), which is null and not zero.
        return VerifyFixAsync(
            """
            class C
            {
                void M(int?[] values)
                {
                    {|E128140:for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = 0;
                    }|}
                }
            }
            """,
            """
            using System;

            class C
            {
                void M(int?[] values)
                {
                    values.AsSpan().Fill(0);
                }
            }
            """);
    }
}
