using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PragmaBundlingCodeFixProviderBranchCoverageMoreTests
{
    private static CSharpCodeFixTest<PragmaBundlingAnalyzer, PragmaBundlingCodeFixProvider, DefaultVerifier> CreateTest(
        string source,
        string fixedCode)
    {
        return new CSharpCodeFixTest<PragmaBundlingAnalyzer, PragmaBundlingCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1,
            TestBehaviors = TestBehaviors.SkipSuppressionCheck,
        };
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

        return CreateTest(source, fixedCode).RunAsync(TestContext.Current.CancellationToken);
    }
}
