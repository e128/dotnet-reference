using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnvalidatedJwtIdentityAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string source)
    {
        return VerifyWithAsync(source, TestStubs.UnvalidatedJwtStubs);
    }

    private static Task VerifyWithAsync(string source, string stubs)
    {
        return AnalyzerTestRunner.VerifyAsync<UnvalidatedJwtIdentityAnalyzer>(
            source,
            options: new AnalyzerTestOptions { Sources = [stubs] });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenClaimsAreAddedToAnIdentityThatIsNotAuthenticated()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class C
            {
                static ClaimsIdentity Create()
                {
                    return new ClaimsIdentity();
                }

                void Empty(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    var identity = new ClaimsIdentity();
                    identity.AddClaims(handler.ReadJwtToken(token).Claims);
                    context.User = new ClaimsPrincipal(identity);
                }

                void FromHelper(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    var identity = Create();
                    identity.AddClaims(handler.ReadJwtToken(token).Claims);
                    context.User = new ClaimsPrincipal(identity);
                }
            }
            """);
    }
}
