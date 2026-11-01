using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class IndexOfAnyToSearchValuesCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<IndexOfAnyToSearchValuesAnalyzer, IndexOfAnyToSearchValuesCodeFixProvider>(source, fixedCode);

    private static Task VerifyNoFixAsync(string source, params DiagnosticResult[] expected) =>
        CodeFixTestRunner.NoFixAsync<IndexOfAnyToSearchValuesAnalyzer, IndexOfAnyToSearchValuesCodeFixProvider>(source, expected: expected);

    [Fact]
    [Trait("Category", "CI")]
    public Task CharArrayCreation_FixedToSearchValues()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M(string path)
                {
                    var i = {|E128102:path.IndexOfAny(new[] { '/', '\\' })|};
                }
            }
            """,
            """
            using System;

            class C
            {
                private static readonly System.Buffers.SearchValues<char> _pathChars = System.Buffers.SearchValues.Create("/\\");

                void M(string path)
                {
                    var i = path.AsSpan().IndexOfAny(_pathChars);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LastIndexOfAny_FixedToSpanLastIndexOfAny()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M(string name)
                {
                    var i = {|E128102:name.LastIndexOfAny(new[] { '.', '-' })|};
                }
            }
            """,
            """
            using System;

            class C
            {
                private static readonly System.Buffers.SearchValues<char> _nameChars = System.Buffers.SearchValues.Create(".-");

                void M(string name)
                {
                    var i = name.AsSpan().LastIndexOfAny(_nameChars);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExistingFirstMember_GetsBlankLineBeforeIt()
    {
        return VerifyFixAsync(
            """
            class C
            {
                private readonly int _count;

                void M(string value)
                {
                    var i = {|E128102:value.IndexOfAny(new[] { 'a', 'b' })|};
                }
            }
            """,
            """
            using System;

            class C
            {
                private static readonly System.Buffers.SearchValues<char> _valueChars = System.Buffers.SearchValues.Create("ab");

                private readonly int _count;

                void M(string value)
                {
                    var i = value.AsSpan().IndexOfAny(_valueChars);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExistingFirstMember_KeepsItsDocComment()
    {
        // The doc comment is leading trivia of the member, so the new field must go above it.
        return VerifyFixAsync(
            """
            class C
            {
                /// <summary>Counts the entries.</summary>
                private readonly int _count;

                void M(string value)
                {
                    var i = {|E128102:value.IndexOfAny(new[] { 'a', 'b' })|};
                }
            }
            """,
            """
            using System;

            class C
            {
                private static readonly System.Buffers.SearchValues<char> _valueChars = System.Buffers.SearchValues.Create("ab");

                /// <summary>Counts the entries.</summary>
                private readonly int _count;

                void M(string value)
                {
                    var i = value.AsSpan().IndexOfAny(_valueChars);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonLiteralArgument_DiagnosticWithoutFix()
    {
        return VerifyNoFixAsync(
            """
            class C
            {
                void M(string value, char[] separators)
                {
                    var i = {|E128102:value.IndexOfAny(separators)|};
                }
            }
            """);
    }
}
