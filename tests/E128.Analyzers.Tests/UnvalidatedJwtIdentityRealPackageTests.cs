using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

/// <summary>
///     Runs E128202 against the real Microsoft.IdentityModel packages. These tests restore from NuGet, so
///     they stay out of the CI category. Run them with <c lang="bash">scripts/test.sh UnvalidatedJwtIdentityRealPackageTests</c>.
/// </summary>
public sealed class UnvalidatedJwtIdentityRealPackageTests
{
    private static Task VerifyAsync(string body)
    {
        return AnalyzerTestRunner.VerifyAsync<UnvalidatedJwtIdentityAnalyzer>(
            $$"""
              using System.Collections.Generic;
              using System.IdentityModel.Tokens.Jwt;
              using System.Linq;
              using System.Security.Claims;
              using System.Threading.Tasks;
              using Microsoft.AspNetCore.Http;
              using Microsoft.IdentityModel.JsonWebTokens;
              using Microsoft.IdentityModel.Tokens;
              class C
              {
                  async Task M(HttpContext context, JwtSecurityTokenHandler handler, JsonWebTokenHandler json, TokenValidationParameters parameters, string token)
                  {
                      {{body}}
                  }
              }
              """,
            options: new AnalyzerTestOptions
            {
                References = TestReferences.Net100WithJwt,
                Sources = [TestStubs.UnvalidatedJwtHttpContext + "\n" + TestStubs.UnvalidatedJwtAuthenticationTicket]
            });
    }

    [Theory]
    [InlineData("{|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\"))|};")]
    [InlineData("{|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(json.ReadJsonWebToken(token).Claims, \"jwt\"))|};")]
    [InlineData("{|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Payload.Claims, \"jwt\"))|};")]
    [InlineData("var claim = json.ReadJsonWebToken(token).GetClaim(\"sub\"); {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity([claim], \"jwt\"))|};")]
    [InlineData("if (json.ReadJsonWebToken(token).TryGetClaim(\"sub\", out var claim)) { {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity([claim], \"jwt\"))|}; }")]
    [InlineData("switch (handler.ReadToken(token)) { case JwtSecurityToken jwt: {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, \"jwt\"))|}; break; }")]
    [Trait("Category", "Manual")]
    public Task DecodedClaims_Fire(string body)
    {
        return VerifyAsync(body);
    }

    [Theory]
    [InlineData("handler.ValidateToken(token, parameters, out _); context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\"));")]
    [InlineData("var result = await json.ValidateTokenAsync(token, parameters); if (!result.IsValid) { return; } context.User = new ClaimsPrincipal(new ClaimsIdentity(json.ReadJsonWebToken(token).Claims, \"jwt\"));")]
    [InlineData("var result = await json.ValidateTokenAsync(token, parameters); if (result.IsValid) { context.User = new ClaimsPrincipal(new ClaimsIdentity(json.ReadJsonWebToken(token).Claims, \"jwt\")); }")]
    [InlineData("var result = await handler.ValidateTokenAsync(token, parameters); if (!result.IsValid) { return; } context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\"));")]
    [InlineData("context.User = handler.ValidateToken(token, parameters, out _);")]
    [Trait("Category", "Manual")]
    public Task ValidatedClaims_Silent(string body)
    {
        return VerifyAsync(body);
    }
}
