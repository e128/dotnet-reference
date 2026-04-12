using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GeneratedRegexCompiledE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<GeneratedRegexAnalyzer, GeneratedRegexCompiledCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            NumberOfFixAllIterations = 1,
        }.RunAsync();
    }

    private const string PartialImpl = "    private static partial System.Text.RegularExpressions.Regex DigitsOnly() => null!;";

    [Fact]
    [Trait("Category", "CI")]
    public Task CompiledAlone_ReplacedWithNone()
    {
        return VerifyFixAsync(
            $$"""
            using System.Text.RegularExpressions;
            using System.Threading;
            partial class C
            {
                [{|E128012:GeneratedRegex(@"\d+", RegexOptions.Compiled, 1000)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """,
            $$"""
            using System.Text.RegularExpressions;
            using System.Threading;
            partial class C
            {
                [GeneratedRegex(@"\d+", RegexOptions.None, 1000)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CompiledOrIgnoreCase_CompiledRemoved()
    {
        return VerifyFixAsync(
            $$"""
            using System.Text.RegularExpressions;
            using System.Threading;
            partial class C
            {
                [{|E128012:GeneratedRegex(@"\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase, 1000)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """,
            $$"""
            using System.Text.RegularExpressions;
            using System.Threading;
            partial class C
            {
                [GeneratedRegex(@"\d+", RegexOptions.IgnoreCase, 1000)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IgnoreCaseOrCompiledOrMultiline_CompiledRemoved()
    {
        return VerifyFixAsync(
            $$"""
            using System.Text.RegularExpressions;
            using System.Threading;
            partial class C
            {
                [{|E128012:GeneratedRegex(@"\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline, 1000)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """,
            $$"""
            using System.Text.RegularExpressions;
            using System.Threading;
            partial class C
            {
                [GeneratedRegex(@"\d+", RegexOptions.IgnoreCase | RegexOptions.Multiline, 1000)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """);
    }
}
