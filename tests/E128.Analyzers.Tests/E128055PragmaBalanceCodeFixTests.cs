using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class E128055PragmaBalanceCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        // Suppress the framework's suppression-check for the same reason as the analyzer
        // tests — E128055 fires on unbalanced pragmas, and the framework adds an
        // unbalanced #pragma warning disable E128055 during its suppression phase.
        return CodeFixTestRunner.RunAsync<PragmaBalanceAnalyzer, PragmaBalanceCodeFixProvider>(
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
    public Task PragmaBalance_CodeFix_InsertsRestoreAtEndOfFile()
    {
        const string source = """
                              {|E128055:#pragma warning disable CS0168|}
                              class Foo { }
                              """;

        // The code fix appends the restore directive as trailing trivia of the last
        // real token. The directive's LineFeed trivia produces one newline after the
        // restore pragma, giving a trailing blank line in the fixed output.
        const string fixedCode = """
                                 #pragma warning disable CS0168
                                 class Foo { }
                                 #pragma warning restore CS0168

                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    /// <summary>
    ///     The bare-disable branch names the suppression with the AllCodes key, so the
    ///     inserted restore carries no error code list. Covers the fixer's other branch.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaBalance_CodeFix_BareDisableInsertsBareRestore()
    {
        const string source = """
                              {|E128055:#pragma warning disable|}
                              class Foo { }
                              """;

        const string fixedCode = """
                                 #pragma warning disable
                                 class Foo { }
                                 #pragma warning restore

                                 """;

        return VerifyFixAsync(source, fixedCode);
    }
}
