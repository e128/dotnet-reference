using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128065PragmaBundlingCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<PragmaBundlingAnalyzer, PragmaBundlingCodeFixProvider>(
            source,
            fixedCode,
            options: new CodeFixTestOptions
            {
                FixAllIterations = 1,
                TestBehaviors = TestBehaviors.SkipSuppressionCheck
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_Fix_SplitsTwoIdsIntoTwoLines()
    {
        const string source = """
                              {|E128065:#pragma warning disable CA2007, MA0004|}
                              class Foo { }
                              """;

        const string fixedCode = """
                                 #pragma warning disable CA2007
                                 #pragma warning disable MA0004
                                 class Foo { }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_Fix_KeepsTheTrailingComment()
    {
        const string source = """
                              {|E128065:#pragma warning disable CA2007, MA0004 // justified|}
                              class Foo { }
                              """;

        const string fixedCode = """
                                 #pragma warning disable CA2007
                                 #pragma warning disable MA0004 // justified
                                 class Foo { }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_Fix_SplitsThreeIdsIntoThreeLines()
    {
        const string source = """
                              {|E128065:#pragma warning disable CA1849, MA0042, MA0045|}
                              class Foo { }
                              """;

        const string fixedCode = """
                                 #pragma warning disable CA1849
                                 #pragma warning disable MA0042
                                 #pragma warning disable MA0045
                                 class Foo { }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
