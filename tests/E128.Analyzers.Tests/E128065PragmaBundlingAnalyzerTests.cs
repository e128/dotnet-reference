using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128065PragmaBundlingAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<PragmaBundlingAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            // Pragma-targeting analyzer: skip the suppression check to avoid the harness
            // injecting #pragma warning disable E128065 and creating self-referential behavior.
            TestBehaviors = TestBehaviors.SkipSuppressionCheck
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_Reports_WhenTwoIdsOnOneLine()
    {
        return VerifyAsync("""
                           {|E128065:#pragma warning disable CA2007, MA0004|}
                           class Foo { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_Reports_WhenThreeIdsOnOneLine()
    {
        return VerifyAsync("""
                           {|E128065:#pragma warning disable CA1849, MA0042, MA0045|}
                           class Foo { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_NoReport_WhenSingleId()
    {
        return VerifyAsync("""
                           #pragma warning disable CA2007
                           class Foo { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_NoReport_WhenBareDisable()
    {
        return VerifyAsync("""
                           #pragma warning disable
                           class Foo { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_NoReport_WhenRestoreHasMultipleIds()
    {
        // restore directives are exempt — only disable is flagged
        return VerifyAsync("""
                           #pragma warning disable CA2007
                           #pragma warning disable MA0004
                           class Foo { }
                           #pragma warning restore CA2007, MA0004
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_Reports_AtDisableOnly_WhenPairPresent()
    {
        // disable fires, restore does not
        return VerifyAsync("""
                           {|E128065:#pragma warning disable CA2007, MA0004|}
                           class Foo { }
                           #pragma warning restore CA2007, MA0004
                           """);
    }
}
