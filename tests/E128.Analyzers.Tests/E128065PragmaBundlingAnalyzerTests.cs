using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128065PragmaBundlingAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { TestBehaviors = TestBehaviors.SkipSuppressionCheck };

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_Reports_WhenTwoIdsOnOneLine()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaBundlingAnalyzer>("""
                           {|E128065:#pragma warning disable CA2007, MA0004|}
                           class Foo { }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_Reports_WhenThreeIdsOnOneLine()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaBundlingAnalyzer>("""
                           {|E128065:#pragma warning disable CA1849, MA0042, MA0045|}
                           class Foo { }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_NoReport_WhenSingleId()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaBundlingAnalyzer>("""
                           #pragma warning disable CA2007
                           class Foo { }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_NoReport_WhenBareDisable()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaBundlingAnalyzer>("""
                           #pragma warning disable
                           class Foo { }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_NoReport_WhenRestoreHasMultipleIds()
    {
        // restore directives are exempt — only disable is flagged
        return AnalyzerTestRunner.VerifyAsync<PragmaBundlingAnalyzer>("""
                           #pragma warning disable CA2007
                           #pragma warning disable MA0004
                           class Foo { }
                           #pragma warning restore CA2007, MA0004
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBundling_Reports_AtDisableOnly_WhenPairPresent()
    {
        // disable fires, restore does not
        return AnalyzerTestRunner.VerifyAsync<PragmaBundlingAnalyzer>("""
                           {|E128065:#pragma warning disable CA2007, MA0004|}
                           class Foo { }
                           #pragma warning restore CA2007, MA0004
                           """, options: Options);
    }
}
