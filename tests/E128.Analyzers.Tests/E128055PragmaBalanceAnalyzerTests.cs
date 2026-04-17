using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128055PragmaBalanceAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<PragmaBalanceAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            // E128055 fires on unbalanced pragmas; the framework's suppression-check phase
            // adds an unbalanced #pragma warning disable E128055, which would cause a
            // self-referential diagnostic. Skip the suppression check to avoid this.
            TestBehaviors = TestBehaviors.SkipSuppressionCheck
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_Reports_WhenPragmaDisableHasNoRestore()
    {
        return VerifyAsync("""
                           {|E128055:#pragma warning disable CS0168|}
                           class Foo { }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_NoReport_WhenPragmaDisableIsRestored()
    {
        return VerifyAsync("""
                           #pragma warning disable CS0168
                           class Foo { }
                           #pragma warning restore CS0168
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_Reports_OnlyUnrestoredIds_WhenMultiIdPragma()
    {
        return VerifyAsync("""
                           {|E128055:#pragma warning disable CA2007, CS0168|}
                           class Foo { }
                           #pragma warning restore CS0168
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_NoReport_WhenBareRestoreCoversAll()
    {
        return VerifyAsync("""
                           #pragma warning disable CS0168
                           #pragma warning disable CA2007
                           class Foo { }
                           #pragma warning restore
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_Reports_WhenMultipleDisablesOnlyOneRestored()
    {
        return VerifyAsync("""
                           {|E128055:#pragma warning disable CA2007|}
                           #pragma warning disable CS0168
                           class Foo { }
                           #pragma warning restore CS0168
                           """);
    }
}
