using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ElementLoopToBulkSpanCodeFixBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<ElementLoopToBulkSpanAnalyzer, ElementLoopToBulkSpanCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_UsesClearOnAnArray()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M(int[] values)
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
                void M(int[] values)
                {
                    values.AsSpan().Clear();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_UsesFillOnASpanWithoutAddingAUsing()
    {
        return VerifyFixAsync(
            """
            using System;

            class C
            {
                void M(Span<int> values, int seed)
                {
                    {|E128140:for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = seed;
                    }|}
                }
            }
            """,
            """
            using System;

            class C
            {
                void M(Span<int> values, int seed)
                {
                    values.Fill(seed);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_CopiesFromASpanWithoutWrappingIt()
    {
        return VerifyFixAsync(
            """
            using System;

            class C
            {
                void M(Span<int> source, int[] destination)
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
                void M(Span<int> source, int[] destination)
                {
                    source.CopyTo(destination);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_CopiesAnArrayIntoASpan()
    {
        return VerifyFixAsync(
            """
            using System;

            class C
            {
                void M(int[] source, Span<int> destination)
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
                void M(int[] source, Span<int> destination)
                {
                    source.AsSpan().CopyTo(destination);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheLoopWithoutBraces()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M(int[] values, int seed)
                {
                    {|E128140:for (var i = 0; i < values.Length; i++)
                        values[i] = seed;|}
                }
            }
            """,
            """
            using System;

            class C
            {
                void M(int[] values, int seed)
                {
                    values.AsSpan().Fill(seed);
                }
            }
            """);
    }
}
