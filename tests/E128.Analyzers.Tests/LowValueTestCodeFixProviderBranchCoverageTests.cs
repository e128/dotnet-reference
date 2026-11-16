using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LowValueTestCodeFixProviderBranchCoverageTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([
            new PackageIdentity("xunit.v3.core", "3.2.2"),
            new PackageIdentity("xunit.v3.assert", "3.2.2")
        ]);

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<LowValueTestAnalyzer, LowValueTestCodeFixProvider>(
            source,
            fixedCode,
            references: Net100WithXunit,
            behaviors: CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            options: new CodeFixTestOptions { FixAllIterations = 1 });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_RemovesTheOnlyMemberOfTheTestClass()
    {
        return VerifyFixAsync(
            """
            using Xunit;

            public static class Calculator
            {
                public static int Echo(int value) => value;
            }

            public sealed class Subject
            {
                [Fact]
                public void {|E128107:Should_SumTwoValues|}()
                {
                    Assert.Equal(1, Calculator.Echo(1));
                }
            }
            """,
            """
            using Xunit;

            public static class Calculator
            {
                public static int Echo(int value) => value;
            }

            public sealed class Subject
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsACommentOnTheClosingBrace()
    {
        return VerifyFixAsync(
            """
            using Xunit;

            public static class Calculator
            {
                public static int Echo(int value) => value;
            }

            public sealed class Subject
            {
                public static int Zero() => 0;

                [Fact]
                public void {|E128107:Should_SumTwoValues|}()
                {
                    Assert.Equal(1, Calculator.Echo(1));
                }
                // End of the subject.
            }
            """,
            """
            using Xunit;

            public static class Calculator
            {
                public static int Echo(int value) => value;
            }

            public sealed class Subject
            {
                public static int Zero() => 0;
                // End of the subject.
            }
            """);
    }
}
