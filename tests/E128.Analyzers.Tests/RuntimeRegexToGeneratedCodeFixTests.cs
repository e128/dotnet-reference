using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RuntimeRegexToGeneratedCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        // The fixed code needs the GeneratedRegex source generator, which the test
        // harness does not run. Compiler diagnostics are off, so every test compares
        // the exact fixed text, including modifier order and trivia.
        return new CSharpCodeFixTest<
            RuntimeRegexToGeneratedAnalyzer,
            RuntimeRegexToGeneratedCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheFieldWithAGeneratedRegexProperty()
    {
        return VerifyFixAsync(
            """
            using System.Text.RegularExpressions;
            class C
            {
                {|E128123:private static readonly Regex Digits = new Regex("[0-9]+");|}
            }
            """,
            """
            using System.Text.RegularExpressions;
            partial class C
            {
                [System.Text.RegularExpressions.GeneratedRegex("[0-9]+", RegexOptions.None, matchTimeoutMilliseconds: System.Threading.Timeout.Infinite)]
                private static partial Regex Digits { get; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_DropsTheCompiledOption()
    {
        return VerifyFixAsync(
            """
            using System.Text.RegularExpressions;
            class C
            {
                {|E128123:private static readonly Regex Digits = new Regex("[0-9]+", RegexOptions.Compiled);|}
            }
            """,
            """
            using System.Text.RegularExpressions;
            partial class C
            {
                [System.Text.RegularExpressions.GeneratedRegex("[0-9]+", RegexOptions.None, matchTimeoutMilliseconds: System.Threading.Timeout.Infinite)]
                private static partial Regex Digits { get; }
            }
            """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_PutsPartialDirectlyBeforeTheTypeKeyword()
    {
        return VerifyFixAsync(
            """
            using System.Text.RegularExpressions;
            public sealed class C
            {
                {|E128123:private static readonly Regex Digits = new Regex("[0-9]+");|}
            }
            """,
            """
            using System.Text.RegularExpressions;
            public sealed partial class C
            {
                [System.Text.RegularExpressions.GeneratedRegex("[0-9]+", RegexOptions.None, matchTimeoutMilliseconds: System.Threading.Timeout.Infinite)]
                private static partial Regex Digits { get; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_MakesEveryEnclosingTypePartial()
    {
        return VerifyFixAsync(
            """
            using System.Text.RegularExpressions;
            public class Outer
            {
                private static string Digits => "x";

                public sealed class Inner
                {
                    {|E128123:private static readonly Regex Digits = new Regex("[0-9]+");|}
                }
            }
            """,
            """
            using System.Text.RegularExpressions;
            public partial class Outer
            {
                private static string Digits => "x";

                public sealed partial class Inner
                {
                    [System.Text.RegularExpressions.GeneratedRegex("[0-9]+", RegexOptions.None, matchTimeoutMilliseconds: System.Threading.Timeout.Infinite)]
                    private static partial Regex Digits { get; }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_DropsCompiledFromAParenthesizedFlagList()
    {
        return VerifyFixAsync(
            """
            using System.Text.RegularExpressions;
            class C
            {
                {|E128123:private static readonly Regex Digits = new Regex("[0-9]+", (RegexOptions.Compiled | RegexOptions.IgnoreCase));|}
            }
            """,
            """
            using System.Text.RegularExpressions;
            partial class C
            {
                [System.Text.RegularExpressions.GeneratedRegex("[0-9]+", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: System.Threading.Timeout.Infinite)]
                private static partial Regex Digits { get; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheOptionsAreNotConstant()
    {
        const string Code = """
            using System.Text.RegularExpressions;
            class C
            {
                private static readonly RegexOptions Opts = RegexOptions.IgnoreCase;
                {|E128123:private static readonly Regex Digits = new Regex("[0-9]+", Opts);|}
            }
            """;

        return VerifyFixAsync(Code, Code);
    }
}
