using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RuntimeRegexToGeneratedBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        // The fixed code needs the GeneratedRegex source generator, which the test harness does not run.
        return new CSharpCodeFixTest<
            RuntimeRegexToGeneratedAnalyzer,
            RuntimeRegexToGeneratedCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync(TestContext.Current.CancellationToken);
    }

    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<RuntimeRegexToGeneratedAnalyzer, RuntimeRegexToGeneratedCodeFixProvider>(
            source,
            behaviors: CodeFixTestBehaviors.None);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_BeforeCSharp13()
    {
        return AnalyzerTestRunner.VerifyAsync<RuntimeRegexToGeneratedAnalyzer>(
            """
            using System.Text.RegularExpressions;
            class C
            {
                private static readonly Regex Digits = new Regex("[0-9]+");
            }
            """,
            options: new AnalyzerTestOptions { LanguageVersion = LanguageVersion.CSharp12 });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenTheFieldShapeCannotBecomeAGeneratedProperty()
    {
        return AnalyzerTestRunner.VerifyAsync<RuntimeRegexToGeneratedAnalyzer>("""
            using System.Text.RegularExpressions;
            class C
            {
                private readonly Regex _instance = new Regex("[0-9]+");

                private static Regex _mutable = new Regex("[0-9]+");

                private static readonly Regex First = new Regex("a"), Second = new Regex("b");

                private static readonly object NotRegex = new object();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Silent_WhenTheConstructionIsNotAPlainPatternLiteral()
    {
        return AnalyzerTestRunner.VerifyAsync<RuntimeRegexToGeneratedAnalyzer>("""
            using System;
            using System.Text.RegularExpressions;
            class C
            {
                private const string Pattern = "[0-9]+";

                private static readonly Regex FromConstant = new Regex(Pattern);

                private static readonly Regex Interpolated = new Regex($"[{Pattern}]");

                private static readonly Regex WithTimeout = new Regex("a", RegexOptions.None, TimeSpan.FromSeconds(1));

                private static readonly Regex Escaped = Escape();

                private static readonly Regex Implicit = new("[0-9]+");

                private static Regex Escape() => new Regex("a");
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_ForStructAndRecordFields()
    {
        return AnalyzerTestRunner.VerifyAsync<RuntimeRegexToGeneratedAnalyzer>("""
            using System.Text.RegularExpressions;
            struct S
            {
                {|E128123:public static readonly Regex Digits = new Regex("[0-9]+");|}
            }

            record R
            {
                {|E128123:internal static readonly Regex Letters = new Regex("[a-z]+");|}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsAnAlreadyPartialTypeAndOtherFlags()
    {
        return VerifyFixAsync(
            """
            using System.Text.RegularExpressions;
            partial class C
            {
                {|E128123:public static readonly Regex Digits = new Regex("[0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);|}
            }
            """,
            """
            using System.Text.RegularExpressions;
            partial class C
            {
                [System.Text.RegularExpressions.GeneratedRegex("[0-9]+", RegexOptions.IgnoreCase | RegexOptions.Multiline, matchTimeoutMilliseconds: System.Threading.Timeout.Infinite)]
                public static partial Regex Digits { get; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ConvertsAFieldOfAStruct()
    {
        return VerifyFixAsync(
            """
            using System.Text.RegularExpressions;
            struct S
            {
                {|E128123:internal static readonly Regex Digits = new Regex("[0-9]+");|}
            }
            """,
            """
            using System.Text.RegularExpressions;
            partial struct S
            {
                [System.Text.RegularExpressions.GeneratedRegex("[0-9]+", RegexOptions.None, matchTimeoutMilliseconds: System.Threading.Timeout.Infinite)]
                internal static partial Regex Digits { get; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheFieldAlreadyHasAnAttribute()
    {
        return VerifyNoFixAsync("""
            using System;
            using System.Text.RegularExpressions;
            class C
            {
                {|E128123:[Obsolete]
                private static readonly Regex Digits = new Regex("[0-9]+");|}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheOptionsAreNotAPlainFlagList()
    {
        return VerifyNoFixAsync("""
            using System.Text.RegularExpressions;
            class C
            {
                {|E128123:private static readonly Regex Digits = new Regex("[0-9]+", (RegexOptions)1 | RegexOptions.None);|}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheOptionsUseAnotherOperator()
    {
        return VerifyNoFixAsync("""
            using System.Text.RegularExpressions;
            class C
            {
                {|E128123:private static readonly Regex Digits = new Regex("[0-9]+", RegexOptions.IgnoreCase & RegexOptions.Compiled);|}
            }
            """);
    }
}
