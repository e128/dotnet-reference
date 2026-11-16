using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SubstringLengthUnderflowBranchCoverageTests
{
    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<SubstringLengthUnderflowAnalyzer>("class C\n{\n" + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForALiteralReceiverWithNoSymbol()
    {
        return VerifyAsync("""
                string M() => {|E128198:"abc".Substring(0, "abc".Length - 1)|};

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForParenthesesAndChainedSubtraction()
    {
        return VerifyAsync("""
                string M(string s) => {|E128198:s.Substring(0, (s.Length - 1))|};

                string N(string s) => {|E128198:s.Substring(0, (s.Length - 1) - 2)|};

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCountIsNotAPositiveLengthSubtraction()
    {
        return VerifyAsync("""
                string A(string s) => s.Substring(0, s.Length + 1);

                string B(string s) => s.Substring(0, s.Length - 0);

                string D(string s, int n) => s.Substring(0, n - 1);
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardFollowsTheCall()
    {
        return VerifyAsync("""
                string M(string s)
                {
                    var head = {|E128198:s.Substring(0, s.Length - 1)|};
                    if (s.Length < 2)
                    {
                        return string.Empty;
                    }

                    return head;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnEqualityTestReadsTheLength()
    {
        return VerifyAsync("""
                string M(string s)
                {
                    if (s.Length != 3)
                    {
                        return s.Substring(0, s.Length - 1);
                    }

                    return s;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheNegatedGuardIsParenthesized()
    {
        return VerifyAsync("""
                string M(string s)
                {
                    if (!(string.IsNullOrEmpty(s)))
                    {
                        return s.Substring(0, s.Length - 1);
                    }

                    return s;
                }

            """);
    }
}
