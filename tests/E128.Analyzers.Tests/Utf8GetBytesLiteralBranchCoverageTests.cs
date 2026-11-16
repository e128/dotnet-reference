using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class Utf8GetBytesLiteralBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<Utf8GetBytesLiteralAnalyzer, Utf8GetBytesLiteralCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_ForOtherEncodingsAndMembers()
    {
        return AnalyzerTestRunner.VerifyAsync<Utf8GetBytesLiteralAnalyzer>("""
            using System;
            using System.Text;
            class Custom
            {
                public static Custom UTF8 => new();

                public byte[] GetBytes(string text) => new byte[1];
            }

            class C
            {
                void M(string value, byte[] buffer)
                {
                    _ = Encoding.ASCII.GetBytes("abc");
                    _ = Encoding.UTF8.GetByteCount("abc");
                    _ = Encoding.UTF8.GetBytes($"abc{value}");
                    _ = Encoding.UTF8.GetBytes("abc".AsSpan(), buffer);
                    _ = Custom.UTF8.GetBytes("abc");
                    _ = Encoding.UTF8.GetString(buffer);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenGetBytesIsNotCalledOnAMemberOfAMember()
    {
        return AnalyzerTestRunner.VerifyAsync<Utf8GetBytesLiteralAnalyzer>("""
            using System.Text;
            class C
            {
                void M(UTF8Encoding utf8)
                {
                    _ = utf8.GetBytes("abc");
                    _ = (Encoding.UTF8).GetBytes("abc");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PassesTheLiteralToAConstructorTakingAByteSpan()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Text;
            class Holder
            {
                public Holder(ReadOnlySpan<byte> bytes)
                {
                }
            }

            class C
            {
                void N()
                {
                    _ = new Holder({|E128141:Encoding.UTF8.GetBytes("abc")|});
                    Holder holder = new({|E128141:Encoding.UTF8.GetBytes("def")|});
                }
            }
            """,
            """
            using System;
            using System.Text;
            class Holder
            {
                public Holder(ReadOnlySpan<byte> bytes)
                {
                }
            }

            class C
            {
                void N()
                {
                    _ = new Holder("abc"u8);
                    Holder holder = new("def"u8);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheArrayWhenAnIndexerTakesIt()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Text;
            class Table
            {
                public int this[byte[] key] => key.Length;
            }

            class C
            {
                int N(Table table)
                {
                    return table[{|E128141:Encoding.UTF8.GetBytes("abc")|}];
                }
            }
            """,
            """
            using System;
            using System.Text;
            class Table
            {
                public int this[byte[] key] => key.Length;
            }

            class C
            {
                int N(Table table)
                {
                    return table["abc"u8.ToArray()];
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheArrayWhenANamedSiblingMakesPositionUnreliable()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Text;
            class C
            {
                void M(ReadOnlySpan<byte> first, int second)
                {
                }

                void N()
                {
                    M({|E128141:Encoding.UTF8.GetBytes("abc")|}, second: 1);
                }
            }
            """,
            """
            using System;
            using System.Text;
            class C
            {
                void M(ReadOnlySpan<byte> first, int second)
                {
                }

                void N()
                {
                    M("abc"u8.ToArray(), second: 1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheArrayForAnArgumentBeyondTheDeclaredParameters()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Text;
            class C
            {
                static void M(byte[] first, params byte[][] more)
                {
                }

                void N(byte[] data)
                {
                    M(data, {|E128141:Encoding.UTF8.GetBytes("abc")|});
                }
            }
            """,
            """
            using System;
            using System.Text;
            class C
            {
                static void M(byte[] first, params byte[][] more)
                {
                }

                void N(byte[] data)
                {
                    M(data, "abc"u8.ToArray());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_UsesTheSpanOnlyForAReadOnlySpanLocal()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Text;
            class C
            {
                void N(byte[] target)
                {
                    ReadOnlySpan<byte> literal = {|E128141:Encoding.UTF8.GetBytes("abc")|};
                    var inferred = {|E128141:Encoding.UTF8.GetBytes("def")|};
                    target = {|E128141:Encoding.UTF8.GetBytes("ghi")|};
                    _ = literal.Length + inferred.Length + target.Length;
                }
            }
            """,
            """
            using System;
            using System.Text;
            class C
            {
                void N(byte[] target)
                {
                    ReadOnlySpan<byte> literal = "abc"u8;
                    var inferred = "def"u8.ToArray();
                    target = "ghi"u8.ToArray();
                    _ = literal.Length + inferred.Length + target.Length;
                }
            }
            """);
    }
}
