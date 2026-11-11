using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128055PragmaBalanceAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { TestBehaviors = TestBehaviors.SkipSuppressionCheck };

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_Reports_WhenPragmaDisableHasNoRestore()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaBalanceAnalyzer>("""
                           {|E128055:#pragma warning disable CS0168|}
                           class Foo { }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_NoReport_WhenPragmaDisableIsRestored()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaBalanceAnalyzer>("""
                           #pragma warning disable CS0168
                           class Foo { }
                           #pragma warning restore CS0168
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_Reports_OnlyUnrestoredIds_WhenMultiIdPragma()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaBalanceAnalyzer>("""
                           {|E128055:#pragma warning disable CA2007, CS0168|}
                           class Foo { }
                           #pragma warning restore CS0168
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_NoReport_WhenBareRestoreCoversAll()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaBalanceAnalyzer>("""
                           #pragma warning disable CS0168
                           #pragma warning disable CA2007
                           class Foo { }
                           #pragma warning restore
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_Reports_WhenRestoreComesBeforeDisable()
    {
        // Directives apply in document order. A restore clears only a disable that precedes it.
        return AnalyzerTestRunner.VerifyAsync<PragmaBalanceAnalyzer>("""
                           #pragma warning restore CS0168
                           {|E128055:#pragma warning disable CS0168|}
                           class Foo { }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_Reports_WhenMultipleDisablesOnlyOneRestored()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaBalanceAnalyzer>("""
                           {|E128055:#pragma warning disable CA2007|}
                           #pragma warning disable CS0168
                           class Foo { }
                           #pragma warning restore CS0168
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_Reports_WhenABareDisableIsNeverRestored()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaBalanceAnalyzer>("""
                           {|E128055:#pragma warning disable|}
                           class Foo { }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_Silent_WhenABareDisableIsRestored()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaBalanceAnalyzer>("""
                           #pragma warning disable
                           class Foo { }
                           #pragma warning restore
                           """, options: Options);
    }
}
