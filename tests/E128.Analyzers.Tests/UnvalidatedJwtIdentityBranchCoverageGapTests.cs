using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnvalidatedJwtIdentityBranchCoverageGapTests
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
    public Task Silent_WhenAnOutVariableLocalIsAssignedLater()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class C
            {
                void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    int.TryParse(token, out var number);
                    number = 3;
                    var parsed = handler.ReadJwtToken(token);
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, ""));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTicketConstructorNamesItsPrincipalDifferently()
    {
        const string ticket = """
            namespace Microsoft.AspNetCore.Authentication
            {
                public class AuthenticationTicket
                {
                    public AuthenticationTicket(System.Security.Claims.ClaimsPrincipal user) { }
                }
            }
            """;

        return VerifyWithAsync(
            """
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Authentication;
            class C
            {
                AuthenticationTicket M(JwtSecurityTokenHandler handler, string token)
                {
                    return new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, "jwt")));
                }
            }
            """,
            string.Join(
                "\n",
                TestStubs.UnvalidatedJwtTokenBase,
                TestStubs.UnvalidatedJwtSecurityToken,
                TestStubs.UnvalidatedJsonWebToken,
                TestStubs.UnvalidatedJwtHttpContext,
                ticket));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTokenIsBuiltFromBytes()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class C
            {
                void M(HttpContext context, byte[] raw)
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(new JwtSecurityToken(raw).Claims, "jwt"));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTokenComesFromReadersOfOtherTypes()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.JsonWebTokens;
            class OwnReader
            {
                public JsonWebToken ReadJsonWebToken(string token) => new();

                public JwtSecurityToken ReadToken(string token) => new();

                public JwtSecurityToken Load(string token) => new();
            }

            class C
            {
                void M(HttpContext context, OwnReader reader, string token)
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(reader.ReadJsonWebToken(token).Claims, "jwt"));
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(reader.ReadToken(token).Claims, "jwt"));
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(reader.Load(token).Claims, "jwt"));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALambdaCapturesAnOutVariableClaimsLocal()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Generic;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class C
            {
                static void Fill(out IEnumerable<Claim> claims) { claims = []; }

                void M(HttpContext context)
                {
                    Fill(out var claims);
                    Action apply = () => context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
                    apply();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALambdaReadsACapturedClaimsLocalTwice()
    {
        return VerifyAsync("""
            using System;
            using System.IdentityModel.Tokens.Jwt;
            using System.Linq;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class C
            {
                void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    var claims = handler.ReadJwtToken(token).Claims;
                    Action apply = () => {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims.Concat(claims), "jwt"))|};
                    apply();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALoopLambdaReadsTheTokenAfterTheUse()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Generic;
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class C
            {
                void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    IEnumerable<Claim> claims = [];
                    while (token.Length > 0)
                    {
                        {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"))|};
                        Action load = () => claims = handler.ReadJwtToken(token).Claims;
                        load();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALambdaRewritesTheValidatedTokenBeforeTheUse()
    {
        return VerifyAsync("""
            using System;
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                Action M(HttpContext context, JwtSecurityTokenHandler handler, string token, string other, TokenValidationParameters parameters)
                {
                    handler.ValidateToken(token, parameters, out _);
                    return () =>
                    {
                        token = other;
                        {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, "jwt"))|};
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALocalFunctionRunsInsideTheValidationGuard()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                async Task M(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    if (result.IsValid)
                    {
                        void Apply()
                        {
                            context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, "jwt"));
                        }

                        Apply();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSinkIsAnEmbeddedStatement()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                void Unvalidated(HttpContext context, JwtSecurityTokenHandler handler, string token, bool flag)
                {
                    var parsed = handler.ReadJwtToken(token);
                    if (flag)
                        {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }

                void Validated(HttpContext context, JwtSecurityTokenHandler handler, string token, bool flag, TokenValidationParameters parameters)
                {
                    handler.ValidateToken(token, parameters, out _);
                    var parsed = handler.ReadJwtToken(token);
                    if (flag)
                        context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALabelPrecedesTheGotoInTheBlock()
    {
        return VerifyAsync("""
            using System;
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                void M(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters)
                {
                    handler.ValidateToken(token, parameters, out _);
                    retry: Console.WriteLine(token);
                    var parsed = handler.ReadJwtToken(token);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                    if (token.Length > 100) goto retry;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTokenIsRewrittenInsideTheValidatingTry()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                void M(HttpContext context, JwtSecurityTokenHandler handler, string token, string other, TokenValidationParameters parameters)
                {
                    try
                    {
                        handler.ValidateToken(token, parameters, out _);
                        token = other;
                    }
                    catch
                    {
                        return;
                    }

                    var parsed = handler.ReadJwtToken(token);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenDeclarationsWithoutASingleInitializerPrecedeTheValidatedUse()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                void M(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters)
                {
                    string unused;
                    int first = 1, second = 2;
                    handler.ValidateToken(token, parameters, out _);
                    var parsed = handler.ReadJwtToken(token);
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheValidationResultComesFromAnOutVariable()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class Validator
            {
                public bool TryValidate(string token, out TokenValidationResult result)
                {
                    result = new();
                    return true;
                }
            }

            class C
            {
                void Failed(HttpContext context, JwtSecurityTokenHandler handler, Validator validator, string token)
                {
                    validator.TryValidate(token, out var result);
                    if (!result.IsValid)
                    {
                        return;
                    }

                    var parsed = handler.ReadJwtToken(token);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }

                void Passed(HttpContext context, JwtSecurityTokenHandler handler, Validator validator, string token)
                {
                    validator.TryValidate(token, out var result);
                    var parsed = handler.ReadJwtToken(token);
                    if (result.IsValid)
                    {
                        {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheBaseIdentityComesFromAnOutArgument()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class C
            {
                static void Make(out ClaimsIdentity identity) { identity = new ClaimsIdentity("jwt"); }

                void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    Make(out var baseIdentity);
                    var parsed = handler.ReadJwtToken(token);
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(baseIdentity, parsed.Claims));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardDoesNotCoverTheUse()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                async Task Rewritten(HttpContext context, JwtSecurityTokenHandler handler, string token, string other, TokenValidationParameters parameters)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    if (result.IsValid)
                    {
                        token = other;
                        var parsed = handler.ReadJwtToken(token);
                        {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                    }
                }

                async Task RewrittenInLoop(HttpContext context, JwtSecurityTokenHandler handler, string token, string other, TokenValidationParameters parameters)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    if (result.IsValid)
                    {
                        for (var i = 0; i < 2; i++)
                        {
                            var parsed = handler.ReadJwtToken(token);
                            {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                            token = other;
                        }
                    }
                }

                async Task OutsideTheGuard(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    if (result.IsValid)
                    {
                        token.ToString();
                    }

                    var parsed = handler.ReadJwtToken(token);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }
            }
            """);
    }
}
