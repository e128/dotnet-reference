using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SearchValuesHoistingCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<SearchValuesHoistingAnalyzer, SearchValuesHoistingCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_HoistsTheTableIntoAStaticReadonlyField()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Buffers;

            class C
            {
                bool M(ReadOnlySpan<char> text)
                {
                    return text.ContainsAny({|E128124:SearchValues.Create("abc")|});
                }
            }
            """,
            """
            using System;
            using System.Buffers;

            class C
            {
                private static readonly System.Buffers.SearchValues<char> _mSearchValues = SearchValues.Create("abc");

                bool M(ReadOnlySpan<char> text)
                {
                    return text.ContainsAny(_mSearchValues);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsTheDocCommentAboveTheFirstMember()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Buffers;

            class C
            {
                /// <summary>Checks the text.</summary>
                bool M(ReadOnlySpan<char> text)
                {
                    return text.ContainsAny({|E128124:SearchValues.Create("abc")|});
                }
            }
            """,
            """
            using System;
            using System.Buffers;

            class C
            {
                private static readonly System.Buffers.SearchValues<char> _mSearchValues = SearchValues.Create("abc");

                /// <summary>Checks the text.</summary>
                bool M(ReadOnlySpan<char> text)
                {
                    return text.ContainsAny(_mSearchValues);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PicksAFreeFieldNameWhenTheDefaultIsTaken()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Buffers;

            class C
            {
                private static readonly int _mSearchValues = 1;

                bool M(ReadOnlySpan<char> text)
                {
                    return text.ContainsAny({|E128124:SearchValues.Create("abc")|});
                }
            }
            """,
            """
            using System;
            using System.Buffers;

            class C
            {
                private static readonly int _mSearchValues = 1;
                private static readonly System.Buffers.SearchValues<char> _mSearchValues2 = SearchValues.Create("abc");

                bool M(ReadOnlySpan<char> text)
                {
                    return text.ContainsAny(_mSearchValues2);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PlacesTheFieldAfterTheStaticFieldItReads()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Buffers;

            class C
            {
                private static readonly string Chars = "abc";

                bool M(ReadOnlySpan<char> text)
                {
                    return text.ContainsAny({|E128124:SearchValues.Create(Chars)|});
                }
            }
            """,
            """
            using System;
            using System.Buffers;

            class C
            {
                private static readonly string Chars = "abc";
                private static readonly System.Buffers.SearchValues<char> _mSearchValues = SearchValues.Create(Chars);

                bool M(ReadOnlySpan<char> text)
                {
                    return text.ContainsAny(_mSearchValues);
                }
            }
            """);
    }
}
