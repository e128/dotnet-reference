using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class Utf8GetBytesLiteralCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<Utf8GetBytesLiteralAnalyzer, Utf8GetBytesLiteralCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheArrayShapeWithToArray()
    {
        return VerifyFixAsync(
            """
            using System.Text;
            class C
            {
                byte[] M()
                {
                    return {|E128141:Encoding.UTF8.GetBytes("abc")|};
                }
            }
            """,
            """
            using System;
            using System.Text;
            class C
            {
                byte[] M()
                {
                    return "abc"u8.ToArray();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PassesTheLiteralToAByteSpanParameter()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Text;
            class C
            {
                void M(ReadOnlySpan<byte> bytes)
                {
                }

                void N()
                {
                    M({|E128141:Encoding.UTF8.GetBytes("abc")|});
                }
            }
            """,
            """
            using System;
            using System.Text;
            class C
            {
                void M(ReadOnlySpan<byte> bytes)
                {
                }

                void N()
                {
                    M("abc"u8);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheArrayShapeForASpanParameter()
    {
        // A u8 literal is a ReadOnlySpan<byte>, so a Span<byte> target needs the array form.
        return VerifyFixAsync(
            """
            using System;
            using System.Text;
            class C
            {
                void M(Span<byte> bytes)
                {
                }

                void N()
                {
                    M({|E128141:Encoding.UTF8.GetBytes("abc")|});
                }
            }
            """,
            """
            using System;
            using System.Text;
            class C
            {
                void M(Span<byte> bytes)
                {
                }

                void N()
                {
                    M("abc"u8.ToArray());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheArrayShapeForASpanLocal()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Text;
            class C
            {
                void N()
                {
                    Span<byte> buffer = {|E128141:Encoding.UTF8.GetBytes("abc")|};
                }
            }
            """,
            """
            using System;
            using System.Text;
            class C
            {
                void N()
                {
                    Span<byte> buffer = "abc"u8.ToArray();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_MapsANamedArgumentToItsParameter()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Text;
            class C
            {
                void M(Span<byte> first, byte[] second)
                {
                }

                void N()
                {
                    M(second: {|E128141:Encoding.UTF8.GetBytes("abc")|}, first: default);
                }
            }
            """,
            """
            using System;
            using System.Text;
            class C
            {
                void M(Span<byte> first, byte[] second)
                {
                }

                void N()
                {
                    M(second: "abc"u8.ToArray(), first: default);
                }
            }
            """);
    }
}
