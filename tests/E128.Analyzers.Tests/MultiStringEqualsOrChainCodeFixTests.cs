using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MultiStringEqualsOrChainCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode, int fixAllIterations = 1)
    {
        return new CSharpCodeFixTest<MultiStringEqualsOrChainAnalyzer, MultiStringEqualsOrChainCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = fixAllIterations,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StringEquals_OI_ThreeChain_ReplacedWithHashSet()
    {
        const string source = """
            using System;
            class C
            {
                bool M(string text) =>
                    {|E128029:string.Equals(text, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "b", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "c", StringComparison.OrdinalIgnoreCase)|};
            }
            """;

        const string fixedCode = """
            using System;
            using System.Collections.Generic;
            class C
            {
                private static readonly HashSet<string> _textValues = new(StringComparer.OrdinalIgnoreCase) { "a", "b", "c" };

                bool M(string text) =>
                    _textValues.Contains(text);
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task EqualsEquals_ThreeChain_ReplacedWithHashSet_OrdinalComparer()
    {
        const string source = """
            class C
            {
                bool M(string text) =>
                    {|E128029:text == "a" || text == "b" || text == "c"|};
            }
            """;

        const string fixedCode = """
            using System;
            using System.Collections.Generic;
            class C
            {
                private static readonly HashSet<string> _textValues = new(StringComparer.Ordinal) { "a", "b", "c" };

                bool M(string text) =>
                    _textValues.Contains(text);
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FourLiteralChain_AllLiteralsExtracted()
    {
        const string source = """
            class C
            {
                bool M(string text) =>
                    {|E128029:text == "a" || text == "b" || text == "c" || text == "d"|};
            }
            """;

        const string fixedCode = """
            using System;
            using System.Collections.Generic;
            class C
            {
                private static readonly HashSet<string> _textValues = new(StringComparer.Ordinal) { "a", "b", "c", "d" };

                bool M(string text) =>
                    _textValues.Contains(text);
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConflictingFieldName_NoFixApplied()
    {
        // If _textValues already exists in the class, the fix should not be offered.
        const string source = """
            using System.Collections.Generic;
            class C
            {
                private static readonly HashSet<string> _textValues = new() { "x" };
                bool M(string text) =>
                    {|E128029:text == "a" || text == "b" || text == "c"|};
            }
            """;

        // No fix offered — diagnostic remains in fixed state.
        const string fixedCode = """
            using System.Collections.Generic;
            class C
            {
                private static readonly HashSet<string> _textValues = new() { "x" };
                bool M(string text) =>
                    {|E128029:text == "a" || text == "b" || text == "c"|};
            }
            """;

        return VerifyFixAsync(source, fixedCode, fixAllIterations: 0);
    }
}
