using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HardcodedSecretAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<HardcodedSecretAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLiteralIsAParameterDefaultOrAMethodExpressionBody()
    {
        return VerifyAsync("""
            class C
            {
                string Password(string secret = "Sup3rS3cret!") => secret;

                string Token() => "Sup3rS3cret!";
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnIndexerGetterReturnsTheLiteral()
    {
        return VerifyAsync("""
            class C
            {
                string this[int index]
                {
                    get => "Sup3rS3cret!";
                }

                string this[string key]
                {
                    get
                    {
                        return "Sup3rS3cret!";
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAssignmentTargetIsParenthesized()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    string password;
                    (password) = "Sup3rS3cret!";
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenATopLevelReturnCarriesTheLiteral()
    {
        return AnalyzerTestRunner.VerifyAsync<HardcodedSecretAnalyzer>(
            """
            return "Sup3rS3cret!";
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

}
