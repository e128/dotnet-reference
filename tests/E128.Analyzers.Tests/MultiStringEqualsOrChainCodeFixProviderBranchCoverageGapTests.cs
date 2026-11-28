using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MultiStringEqualsOrChainCodeFixProviderBranchCoverageGapTests
{
    private static Task VerifyNoFixAsync(string source, OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary)
    {
        return CodeFixTestRunner.NoFixAsync<MultiStringEqualsOrChainAnalyzer, MultiStringEqualsOrChainCodeFixProvider>(
            source,
            options: new CodeFixTestOptions { OutputKind = outputKind });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAnOperandOfTheChainIsNotAStringTest()
    {
        return CodeFixTestRunner.NoFixAsync<MultiStringEqualsOrChainAnalyzer, MultiStringEqualsOrChainCodeFixProvider>(
            """
            class C
            {
                bool M(string text, bool flag) =>
                    {|E128029:text == "a" || text == "b" || text == "c" || flag|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAnOperandTestsAnotherValue()
    {
        return CodeFixTestRunner.NoFixAsync<MultiStringEqualsOrChainAnalyzer, MultiStringEqualsOrChainCodeFixProvider>(
            """
            class C
            {
                bool M(string text, string other) =>
                    {|E128029:text == "a" || text == "b" || text == "c" || other == "d"|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAnOperandUsesAnotherComparison()
    {
        return CodeFixTestRunner.NoFixAsync<MultiStringEqualsOrChainAnalyzer, MultiStringEqualsOrChainCodeFixProvider>(
            """
            using System;
            class C
            {
                bool M(string text) =>
                    {|E128029:text == "a" || text == "b" || text == "c" || string.Equals(text, "d", StringComparison.Ordinal)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheChainSitsInTopLevelStatements()
    {
        return VerifyNoFixAsync(
            """
            using System;

            var text = args.Length > 0 ? args[0] : string.Empty;
            var hit = {|E128029:text == "a" || text == "b" || text == "c"|};
            Console.WriteLine(hit);
            """,
            OutputKind.ConsoleApplication);
    }
}
