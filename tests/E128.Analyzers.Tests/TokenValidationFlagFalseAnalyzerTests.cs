using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TokenValidationFlagFalseAnalyzerTests
{
    private const string Stub = """
                                namespace Microsoft.IdentityModel.Tokens
                                {
                                    public class TokenValidationParameters
                                    {
                                        public bool ValidateIssuer { get; set; }
                                        public bool ValidateAudience { get; set; }
                                        public bool ValidateLifetime { get; set; }
                                        public bool ValidateIssuerSigningKey { get; set; }
                                        public bool RequireExpirationTime { get; set; }
                                        public bool RequireSignedTokens { get; set; }
                                    }
                                }
                                """;

    private static readonly AnalyzerTestOptions Options = new() { Sources = [Stub] };

    private static DiagnosticResult Expected()
    {
        return new DiagnosticResult("E128228", DiagnosticSeverity.Warning).WithLocation(0);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("ValidateIssuer")]
    [InlineData("ValidateAudience")]
    [InlineData("ValidateLifetime")]
    [InlineData("RequireExpirationTime")]
    [InlineData("RequireSignedTokens")]
    public Task Reports_WhenAValidationFlagIsSetToFalse(string flag)
    {
        return AnalyzerTestRunner.VerifyAsync<TokenValidationFlagFalseAnalyzer>(
            $$"""
              using Microsoft.IdentityModel.Tokens;

              class C
              {
                  TokenValidationParameters M() => new() { {|#0:{{flag}} = false|} };
              }
              """,
            Expected(),
            Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WithItsOwnMessage_WhenTheSigningKeyFlagIsSetToFalse()
    {
        const string Code = """
                            using Microsoft.IdentityModel.Tokens;

                            class C
                            {
                                TokenValidationParameters M() => new() { {|#0:ValidateIssuerSigningKey = false|} };
                            }
                            """;

        var expected = Expected().WithMessage("ValidateIssuerSigningKey is set to false, which is the default, so the token handler does not check that the signing key belongs to the issuer");

        return AnalyzerTestRunner.VerifyAsync<TokenValidationFlagFalseAnalyzer>(Code, expected, Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAFlagIsAssignedAfterConstruction()
    {
        return AnalyzerTestRunner.VerifyAsync<TokenValidationFlagFalseAnalyzer>(
            """
            using Microsoft.IdentityModel.Tokens;

            class C
            {
                void M(TokenValidationParameters parameters)
                {
                    {|#0:parameters.ValidateLifetime = false|};
                }
            }
            """,
            Expected(),
            Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFalseLiteralIsParenthesized()
    {
        return AnalyzerTestRunner.VerifyAsync<TokenValidationFlagFalseAnalyzer>(
            """
            using Microsoft.IdentityModel.Tokens;

            class C
            {
                TokenValidationParameters M() => new() { {|#0:ValidateAudience = (false)|} };
            }
            """,
            Expected(),
            Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenADerivedTypeSetsTheFlag()
    {
        return AnalyzerTestRunner.VerifyAsync<TokenValidationFlagFalseAnalyzer>(
            """
            using Microsoft.IdentityModel.Tokens;

            class MyParameters : TokenValidationParameters
            {
            }

            class C
            {
                MyParameters M() => new() { {|#0:ValidateIssuer = false|} };
            }
            """,
            Expected(),
            Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFlagIsTrue()
    {
        return AnalyzerTestRunner.VerifyAsync<TokenValidationFlagFalseAnalyzer>(
            """
            using Microsoft.IdentityModel.Tokens;

            class C
            {
                TokenValidationParameters M() => new() { ValidateAudience = true };
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheValueIsALocal()
    {
        return AnalyzerTestRunner.VerifyAsync<TokenValidationFlagFalseAnalyzer>(
            """
            using Microsoft.IdentityModel.Tokens;

            class C
            {
                TokenValidationParameters M()
                {
                    var disabled = false;
                    return new() { ValidateAudience = disabled };
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheValueIsAConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<TokenValidationFlagFalseAnalyzer>(
            """
            using Microsoft.IdentityModel.Tokens;

            class C
            {
                TokenValidationParameters M()
                {
                    const bool Disabled = false;
                    return new() { ValidateAudience = Disabled };
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFlagIsPassedAsANamedArgument()
    {
        return AnalyzerTestRunner.VerifyAsync<TokenValidationFlagFalseAnalyzer>(
            """
            class C
            {
                void M()
                {
                    Configure(validateAudience: false);
                }

                void Configure(bool validateAudience)
                {
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnUnrelatedTypeHasTheSameProperty()
    {
        return AnalyzerTestRunner.VerifyAsync<TokenValidationFlagFalseAnalyzer>(
            """
            class Settings
            {
                public bool ValidateAudience { get; set; }
            }

            class C
            {
                Settings M() => new() { ValidateAudience = false };
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTokenValidationParametersTypeIsNotReferenced()
    {
        return AnalyzerTestRunner.VerifyAsync<TokenValidationFlagFalseAnalyzer>(
            """
            namespace Other
            {
                public class TokenValidationParameters
                {
                    public bool ValidateAudience { get; set; }
                }
            }

            class C
            {
                Other.TokenValidationParameters M() => new() { ValidateAudience = false };
            }
            """);
    }
}
