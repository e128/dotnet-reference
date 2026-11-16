using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaticNumericIncrementCodeFixProviderBranchCoverageGapTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RemovesTheOnlyModifierOfTheField()
    {
        return CodeFixTestRunner.RunAsync<StaticNumericIncrementAnalyzer, StaticNumericIncrementCodeFixProvider>(
            "class C { static int _total; int M() { return _total {|E128087:+=|} 2; } }",
            "class C { int _total; int M() { return _total += 2; } }");
    }
}
