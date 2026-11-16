using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GeneratedRegexAnalyzerBranchCoverageMoreTests
{
    private static AnalyzerTestOptions Lenient => new() { CompilerDiagnostics = CompilerDiagnostics.None };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheAttributeConstructorTakesNoArguments()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>(
            """
            namespace System.Text.RegularExpressions
            {
                [System.AttributeUsage(System.AttributeTargets.Method)]
                public sealed class GeneratedRegexAttribute : System.Attribute
                {
                    public GeneratedRegexAttribute() { }
                }
            }
            class C
            {
                [{|E128011:System.Text.RegularExpressions.GeneratedRegex|}]
                static void First() { }

                [{|E128011:System.Text.RegularExpressions.GeneratedRegex()|}]
                static void Second() { }
            }
            """,
            options: Lenient);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenANamedPropertyArgumentFollowsAPositionalTimeout()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>(
            """
            using System.Text.RegularExpressions;
            partial class C
            {
                [GeneratedRegex(@"\d+", RegexOptions.None, 1000, Other = 1)]
                private static partial Regex Probe();
                private static partial Regex Probe() => null!;
            }
            """,
            options: Lenient);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePatternIsANullLiteral()
    {
        return AnalyzerTestRunner.VerifyAsync<GeneratedRegexAnalyzer>(
            """
            using System.Text.RegularExpressions;
            partial class C
            {
                [GeneratedRegex(null, RegexOptions.None, 1000)]
                private static partial Regex Probe();
                private static partial Regex Probe() => null!;
            }
            """,
            options: Lenient);
    }
}
