using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Covers how the GeneratedRegex analyzer picks attributes and reads their arguments.
/// </summary>
public sealed class GeneratedRegexBranchCoverageTests
{
    private const string PartialImpl = "    private static partial System.Text.RegularExpressions.Regex Probe() => null!;";

    private static Task VerifyNoDiagnosticAsync(string source, bool compilerDiagnostics = true)
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>(
            source,
            options: compilerDiagnostics
                ? null
                : new AnalyzerTestOptions { CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FullAttributeTypeName_WithoutTimeout_ReportsTimeout()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [{|E128011:GeneratedRegexAttribute(@"\d+")|}]
                                 private static partial Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task QualifiedAttributeName_WithoutTimeout_ReportsTimeout()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>($$"""
                             partial class C
                             {
                                 [{|E128011:System.Text.RegularExpressions.GeneratedRegex(@"\d+")|}]
                                 private static partial System.Text.RegularExpressions.Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamedTimeoutArgument_ReportsNothing()
    {
        return VerifyNoDiagnosticAsync($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [GeneratedRegex(@"\d+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
                                 private static partial Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamedOptionsOnly_ReportsMissingTimeout()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [{|E128011:GeneratedRegex(@"\d+", options: RegexOptions.IgnoreCase)|}]
                                 private static partial Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CompiledWithNamedTimeout_ReportsCompiled()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [{|E128012:GeneratedRegex(@"\d+", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)|}]
                                 private static partial Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CompiledInNamedOptions_ReportsCompiled()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [{|E128012:GeneratedRegex(@"\d+", options: RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)|}]
                                 private static partial Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamedOptionsWithoutCompiled_ReportsNothing()
    {
        return VerifyNoDiagnosticAsync($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [GeneratedRegex(@"\d+", options: RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
                                 private static partial Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DefaultOptionsExpression_ReportsNothing()
    {
        return VerifyNoDiagnosticAsync($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [GeneratedRegex(@"\d+", default, 1000)]
                                 private static partial Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CompiledNamedFieldOfAnotherType_ReportsNothing()
    {
        return VerifyNoDiagnosticAsync($$"""
                             using System.Text.RegularExpressions;
                             static class Flags
                             {
                                 public const RegexOptions Compiled = RegexOptions.None;
                             }
                             partial class C
                             {
                                 [GeneratedRegex(@"\d+", Flags.Compiled, 1000)]
                                 private static partial Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CompiledFieldOfLookAlikeRegexOptionsType_ReportsNothing()
    {
        return VerifyNoDiagnosticAsync($$"""
                             using System.Text.RegularExpressions;
                             namespace Fake
                             {
                                 static class RegexOptions
                                 {
                                     public const System.Text.RegularExpressions.RegexOptions Compiled =
                                         System.Text.RegularExpressions.RegexOptions.None;
                                 }
                             }
                             partial class C
                             {
                                 [GeneratedRegex(@"\d+", Fake.RegexOptions.Compiled, 1000)]
                                 private static partial Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NamedPatternArgument_SkipsQuantifierRules()
    {
        return VerifyNoDiagnosticAsync($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [GeneratedRegex(pattern: @"(a+)+", options: RegexOptions.None, matchTimeoutMilliseconds: 1000)]
                                 private static partial Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstantPatternReference_SkipsQuantifierRules()
    {
        return VerifyNoDiagnosticAsync($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 private const string Pattern = "(a+)+";

                                 [GeneratedRegex(Pattern, RegexOptions.None, 1000)]
                                 private static partial Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConcatenatedPattern_SkipsQuantifierRules()
    {
        return VerifyNoDiagnosticAsync($$"""
                             using System.Text.RegularExpressions;
                             partial class C
                             {
                                 [GeneratedRegex("(a+)" + "+", RegexOptions.None, 1000)]
                                 private static partial Regex Probe();
                                 {{PartialImpl}}
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AttributeThatDoesNotResolve_ReportsNothing()
    {
        return VerifyNoDiagnosticAsync(
            """
            partial class C
            {
                [GeneratedRegex(@"\d+")]
                private static partial int Probe();
            }
            """,
            compilerDiagnostics: false);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AliasQualifiedAttribute_ReportsNothing()
    {
        return VerifyNoDiagnosticAsync(
            """
            class MarkerAttribute : System.Attribute
            {
            }

            [global::Marker]
            class C
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AliasToAnotherAttributeType_ReportsNothing()
    {
        return VerifyNoDiagnosticAsync(
            """
            using GeneratedRegex = System.ObsoleteAttribute;

            [GeneratedRegex("old")]
            class C
            {
            }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Fake")]
    [InlineData("Fake.RegularExpressions")]
    [InlineData("Fake.Text.RegularExpressions")]
    [InlineData("Outer.System.Text.RegularExpressions")]
    [InlineData("System.Fake.RegularExpressions")]
    public Task LookAlikeAttributeInAnotherNamespace_ReportsNothing(string attributeNamespace)
    {
        return VerifyNoDiagnosticAsync($$"""
                             namespace {{attributeNamespace}}
                             {
                                 public sealed class GeneratedRegexAttribute : global::System.Attribute
                                 {
                                     public GeneratedRegexAttribute(string pattern)
                                     {
                                         Pattern = pattern;
                                     }

                                     public string Pattern { get; }
                                 }
                             }

                             [{{attributeNamespace}}.GeneratedRegex("(a+)+")]
                             class C
                             {
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LookAlikeAttributeInGlobalNamespace_ReportsNothing()
    {
        return VerifyNoDiagnosticAsync(
            """
            public sealed class GeneratedRegexAttribute : global::System.Attribute
            {
                public GeneratedRegexAttribute(string pattern)
                {
                    Pattern = pattern;
                }

                public string Pattern { get; }
            }

            [GeneratedRegex("(a+)+")]
            class C
            {
            }
            """);
    }
}
