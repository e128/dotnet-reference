using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class GeneratedRegexTimeoutE128CodeFixTests
{
    private const string PartialImpl = "    private static partial System.Text.RegularExpressions.Regex DigitsOnly() => null!;";

    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<GeneratedRegexAnalyzer, GeneratedRegexTimeoutCodeFixProvider>(source, fixedCode);

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

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheAttributeAlreadyUsesANamedArgument()
    {
        const string source = $$"""
                                using System.Text.RegularExpressions;
                                partial class C
                                {
                                    [{|E128011:GeneratedRegex(@"\d+", options: RegexOptions.None)|}]
                                    private static partial Regex DigitsOnly();
                                    {{PartialImpl}}
                                }
                                """;

        // A positional argument cannot follow a named one, so the appended pair would not compile.
        var test = new CSharpCodeFixTest<GeneratedRegexAnalyzer, GeneratedRegexTimeoutCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllInDocumentIterations = 0
        };

        return test.RunAsync();
    }
}
