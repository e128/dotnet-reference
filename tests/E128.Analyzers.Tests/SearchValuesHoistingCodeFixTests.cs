using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SearchValuesHoistingCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<
            SearchValuesHoistingAnalyzer,
            SearchValuesHoistingCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

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
                private static readonly System.Buffers.SearchValues<char> _mSearchValues2 = SearchValues.Create("abc");
                private static readonly int _mSearchValues = 1;

                bool M(ReadOnlySpan<char> text)
                {
                    return text.ContainsAny(_mSearchValues2);
                }
            }
            """);
    }
}
