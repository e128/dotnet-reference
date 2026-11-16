using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnvalidatedJwtIdentityAnalyzerBranchCoverageMoreTests
{
    private const string TokenBaseStub = """
        namespace Microsoft.IdentityModel.Tokens
        {
            public class SecurityToken { }
            public class TokenValidationParameters { }
            public class TokenValidationResult { public bool IsValid { get; set; } }
        }
        """;

    private const string JwtStub = """
        namespace System.IdentityModel.Tokens.Jwt
        {
            public class JwtSecurityToken : Microsoft.IdentityModel.Tokens.SecurityToken
            {
                public JwtSecurityToken() { }
                public JwtSecurityToken(string token) { }
                public JwtSecurityToken(byte[] raw) { }
                public System.Collections.Generic.IEnumerable<System.Security.Claims.Claim> Claims => [];
            }
            public class JwtSecurityTokenHandler
            {
                public virtual JwtSecurityToken ReadJwtToken(string token) => new();
                public virtual Microsoft.IdentityModel.Tokens.SecurityToken ReadToken(string token) => new JwtSecurityToken();
                public virtual System.Security.Claims.ClaimsPrincipal ValidateToken(string token, Microsoft.IdentityModel.Tokens.TokenValidationParameters validationParameters, out Microsoft.IdentityModel.Tokens.SecurityToken validatedToken)
                {
                    validatedToken = new();
                    return new();
                }
                public virtual System.Threading.Tasks.Task<Microsoft.IdentityModel.Tokens.TokenValidationResult> ValidateTokenAsync(string token, Microsoft.IdentityModel.Tokens.TokenValidationParameters validationParameters) => System.Threading.Tasks.Task.FromResult(new Microsoft.IdentityModel.Tokens.TokenValidationResult());
            }
        }
        """;

    private const string JsonStub = """
        namespace Microsoft.IdentityModel.JsonWebTokens
        {
            public class JsonWebToken : Microsoft.IdentityModel.Tokens.SecurityToken
            {
                public System.Collections.Generic.IEnumerable<System.Security.Claims.Claim> Claims => [];
            }
            public class JsonWebTokenHandler
            {
                public virtual JsonWebToken ReadJsonWebToken(string token) => new();
                public virtual Microsoft.IdentityModel.Tokens.TokenValidationResult ValidateToken(string token, Microsoft.IdentityModel.Tokens.TokenValidationParameters validationParameters) => new();
            }
        }
        """;

    private const string HttpStub = """
        namespace Microsoft.AspNetCore.Http
        {
            public abstract class HttpContext
            {
                public abstract System.Security.Claims.ClaimsPrincipal User { get; set; }
            }
        }
        """;

    private const string TicketStub = """
        namespace Microsoft.AspNetCore.Authentication
        {
            public class AuthenticationTicket
            {
                public AuthenticationTicket(System.Security.Claims.ClaimsPrincipal principal, string authenticationScheme) { }
            }
        }
        """;

    private static readonly string FullStubs = string.Join("\n", TokenBaseStub, JwtStub, JsonStub, HttpStub, TicketStub);

    private static Task VerifyAsync(string source)
    {
        return VerifyWithAsync(source, FullStubs);
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
