using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OceWhenTokenFilterBranchCoverageTests
{
    private static Task VerifyAsync(string catchClauses)
    {
        return AnalyzerTestRunner.VerifyAsync<OceWhenTokenFilterAnalyzer>(
            "using System;\nusing System.Threading;\nusing System.Threading.Tasks;\n"
            + "struct FakeToken { public bool IsCancellationRequested => false; }\n"
            + "class C\n{\n    void M(CancellationToken token, FakeToken fake, bool flag)\n    {\n        try { }\n"
            + catchClauses + "\n    }\n}\n");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("TaskCanceledException")]
    [InlineData("System.OperationCanceledException")]
    public Task Reports_ForTheCancellationExceptionTypes(string type)
    {
        return VerifyAsync($$"""
                    {|E128100:catch ({{type}}) when (!token.IsCancellationRequested)|}
                    {
                        throw;
                    }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("(!token.IsCancellationRequested)")]
    [InlineData("((!(token.IsCancellationRequested)))")]
    public Task Reports_ThroughParentheses(string filter)
    {
        return VerifyAsync($$"""
                    {|E128100:catch (OperationCanceledException) when ({{filter}})|}
                    {
                        throw;
                    }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForACatchWithoutADeclarationOrFilter()
    {
        return VerifyAsync("""
                    catch when (!token.IsCancellationRequested)
                    {
                    }
                    catch (OperationCanceledException)
                    {
                    }
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("catch (InvalidOperationException) when (!token.IsCancellationRequested) { }")]
    [InlineData("catch (OperationCanceledException) when (token.IsCancellationRequested) { }")]
    [InlineData("catch (OperationCanceledException) when (!fake.IsCancellationRequested) { }")]
    [InlineData("catch (OperationCanceledException) when (!token.CanBeCanceled) { }")]
    [InlineData("catch (OperationCanceledException) when (!flag) { }")]
    [InlineData("catch (OperationCanceledException) when (flag) { }")]
    public Task Silent_ForFiltersThatAreNotTheNegatedTokenState(string clause)
    {
        return VerifyAsync(clause);
    }
}
