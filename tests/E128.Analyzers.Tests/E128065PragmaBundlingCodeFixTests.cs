using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128065PragmaBundlingCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        var test = new CSharpCodeFixTest<PragmaBundlingAnalyzer, PragmaBundlingCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1,
            TestBehaviors = TestBehaviors.SkipSuppressionCheck
        };
        return test.RunAsync();
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
