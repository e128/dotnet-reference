using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SearchValuesHoistingCodeFixProviderBranchCoverageMoreTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<SearchValuesHoistingAnalyzer, SearchValuesHoistingCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_UsesTheFallbackNameAndKeepsAFirstInstanceFieldTogetherWithTheNewField()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Buffers;

            class C
            {
                private readonly int _count;

                public bool Has => "abc".AsSpan().ContainsAny({|E128124:SearchValues.Create("abc")|});
            }
            """,
            """
            using System;
            using System.Buffers;

            class C
            {
                private static readonly System.Buffers.SearchValues<char> _searchValues = SearchValues.Create("abc");
                private readonly int _count;

                public bool Has => "abc".AsSpan().ContainsAny(_searchValues);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_HandlesAFirstMemberThatHasNoIndentation()
    {
        return VerifyFixAsync(
            "using System;\nusing System.Buffers;\n\nclass C\n{\n\nbool M(ReadOnlySpan<char> text)\n{\nreturn text.ContainsAny({|E128124:SearchValues.Create(\"abc\")|});\n}\n}\n",
            "using System;\nusing System.Buffers;\n\nclass C\n{\n    private static readonly System.Buffers.SearchValues<char> _mSearchValues = SearchValues.Create(\"abc\");\n\n    bool M(ReadOnlySpan<char> text)\n{\nreturn text.ContainsAny(_mSearchValues);\n}\n}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheCallIsInTopLevelStatements()
    {
        return CodeFixTestRunner.NoFixAsync<SearchValuesHoistingAnalyzer, SearchValuesHoistingCodeFixProvider>(
            """
            using System;
            using System.Buffers;

            ReadOnlySpan<char> text = "abc";
            var found = text.ContainsAny({|E128124:SearchValues.Create("abc")|});
            """,
            null,
            CodeFixTestBehaviors.None,
            DiagnosticResult.CompilerError("CS8805").WithLocation(4, 1));
    }
}
