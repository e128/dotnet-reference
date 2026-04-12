using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GeneratedRegexTimeoutE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<GeneratedRegexAnalyzer, GeneratedRegexTimeoutCodeFixProvider, DefaultVerifier>
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
    public Task PatternOnly_InsertsOptionsAndTimeout()
    {
        var source = $$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128011:GeneratedRegex(@"\d+")|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """;

        var fixedCode = $$"""
            using System.Text.RegularExpressions;
            using System.Threading;

            partial class C
            {
                [GeneratedRegex(@"\d+", RegexOptions.None, Timeout.Infinite)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PatternAndOptions_AppendsTimeout()
    {
        var source = $$"""
            using System.Text.RegularExpressions;
            partial class C
            {
                [{|E128011:GeneratedRegex(@"\d+", RegexOptions.None)|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """;

        var fixedCode = $$"""
            using System.Text.RegularExpressions;
            using System.Threading;

            partial class C
            {
                [GeneratedRegex(@"\d+", RegexOptions.None, Timeout.Infinite)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UsingAlreadyPresent_DoesNotDuplicate()
    {
        var source = $$"""
            using System.Text.RegularExpressions;
            using System.Threading;
            partial class C
            {
                [{|E128011:GeneratedRegex(@"\d+")|}]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """;

        var fixedCode = $$"""
            using System.Text.RegularExpressions;
            using System.Threading;
            partial class C
            {
                [GeneratedRegex(@"\d+", RegexOptions.None, Timeout.Infinite)]
                private static partial Regex DigitsOnly();
                {{PartialImpl}}
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }
}
