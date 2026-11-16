using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TimingSafeComparisonCodeFixBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheDiagnosticIsAStringComparison()
    {
        return CodeFixTestRunner.NoFixAsync<TimingSafeComparisonAnalyzer, TimingSafeComparisonCodeFixProvider>(
            """
            class C
            {
                bool M(string computedTag, string name)
                {
                    return {|E128157:computedTag == name|};
                }

                bool Call(string computedTag, string name)
                {
                    return Check({|E128157:computedTag == name|});
                }

                bool Check(bool value) => value;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheDiagnosticIsAStringEqualsCall()
    {
        return CodeFixTestRunner.NoFixAsync<TimingSafeComparisonAnalyzer, TimingSafeComparisonCodeFixProvider>(
            """
            class C
            {
                bool M(string computedTag, string name)
                {
                    return {|E128157:string.Equals(computedTag, name)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenOnlyTheSecondOperandMayBeNull()
    {
        return CodeFixTestRunner.NoFixAsync<TimingSafeComparisonAnalyzer, TimingSafeComparisonCodeFixProvider>(
            """
            #nullable enable
            using System.Linq;

            class C
            {
                byte[]? Maybe(byte[]? value) => value;

                bool M(byte[] computedTag, byte[]? expectedTag)
                {
                    return {|E128157:computedTag.SequenceEqual(Maybe(expectedTag))|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_KeepsTheExistingUsingAndTrivia()
    {
        return CodeFixTestRunner.RunAsync<TimingSafeComparisonAnalyzer, TimingSafeComparisonCodeFixProvider>(
            """
            using System.Linq;
            using System.Security.Cryptography;

            class C
            {
                bool M(byte[] computedTag, byte[] expectedTag)
                {
                    return !{|E128157:computedTag.SequenceEqual(expectedTag)|}; // compare
                }
            }
            """,
            """
            using System.Linq;
            using System.Security.Cryptography;

            class C
            {
                bool M(byte[] computedTag, byte[] expectedTag)
                {
                    return !CryptographicOperations.FixedTimeEquals(computedTag, expectedTag); // compare
                }
            }
            """);
    }
}
