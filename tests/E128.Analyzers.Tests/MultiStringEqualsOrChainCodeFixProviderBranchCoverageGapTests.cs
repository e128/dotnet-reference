using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MultiStringEqualsOrChainCodeFixProviderBranchCoverageGapTests
{
    private static Task VerifyNoFixAsync(string source, OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary)
    {
        var test = new CSharpCodeFixTest<MultiStringEqualsOrChainAnalyzer, MultiStringEqualsOrChainCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.TestState.OutputKind = outputKind;

        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAnOperandOfTheChainIsNotAStringTest()
    {
        return VerifyNoFixAsync(
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
        return VerifyNoFixAsync(
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
        return VerifyNoFixAsync(
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
