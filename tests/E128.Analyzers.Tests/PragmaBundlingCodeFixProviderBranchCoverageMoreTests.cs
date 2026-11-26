using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PragmaBundlingCodeFixProviderBranchCoverageMoreTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<PragmaBundlingAnalyzer, PragmaBundlingCodeFixProvider>(
            source,
            fixedCode,
            options: new CodeFixTestOptions
            {
                FixAllIterations = 1,
                TestBehaviors = TestBehaviors.SkipSuppressionCheck,
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_LeavesAnEarlierPragmaOfTheSameLeadingTriviaUntouched()
    {
        const string source = """
                              #pragma warning disable CA2007
                              {|E128065:#pragma warning disable MA0004, MA0045|}
                              class Foo { }
                              """;

        const string fixedCode = """
                                 #pragma warning disable CA2007
                                 #pragma warning disable MA0004
                                 #pragma warning disable MA0045
                                 class Foo { }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
