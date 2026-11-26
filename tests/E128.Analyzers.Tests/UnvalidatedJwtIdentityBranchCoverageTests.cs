using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnvalidatedJwtIdentityBranchCoverageTests
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
    public Task Reports_AJsonHandlerTicketWhenNoJwtHandlerOrHttpContextExists()
    {
        return VerifyWithAsync(
            """
            using System.Security.Claims;
            using Microsoft.AspNetCore.Authentication;
            using Microsoft.IdentityModel.JsonWebTokens;
            class C
            {
                AuthenticationTicket M(JsonWebTokenHandler handler, string token)
                {
                    return {|E128202:new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJsonWebToken(token).Claims, "jwt")), "jwt")|};
                }
            }
            """,
            string.Join("\n", TestStubs.UnvalidatedJwtTokenBase, TestStubs.UnvalidatedJsonWebToken, TestStubs.UnvalidatedJwtAuthenticationTicket));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNoTokenHandlerTypeExists()
    {
        return VerifyWithAsync(
            """
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class C
            {
                void M(HttpContext context, ClaimsIdentity identity)
                {
                    context.User = new ClaimsPrincipal(identity);
                }
            }
            """,
            string.Join("\n", TestStubs.UnvalidatedJwtTokenBase, TestStubs.UnvalidatedJwtHttpContext));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNeitherAnHttpContextNorATicketTypeExists()
    {
        return VerifyWithAsync(
            """
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            class C
            {
                ClaimsPrincipal M(JwtSecurityTokenHandler handler, string token)
                {
                    return new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, "jwt"));
                }
            }
            """,
            string.Join("\n", TestStubs.UnvalidatedJwtTokenBase, TestStubs.UnvalidatedJwtSecurityToken));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ADecodedTokenReadInAnIfElseBranch()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class C
            {
                void M(HttpContext context, JwtSecurityTokenHandler handler, string token, bool flag)
                {
                    var parsed = handler.ReadJwtToken(token);
                    if (flag)
                    {
                        context.User = new ClaimsPrincipal();
                    }
                    else
                    {
                        {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheIdentityIsUnauthenticatedByAnEmptyOrNullType()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class C
            {
                void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    var parsed = handler.ReadJwtToken(token);
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, ""));
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, null));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePropertyIsNotTheUserOfAnHttpContext()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            class Session
            {
                public ClaimsPrincipal? User { get; set; }

                public ClaimsPrincipal? Other { get; set; }
            }

            class C
            {
                void M(Session session, JwtSecurityTokenHandler handler, string token)
                {
                    var parsed = handler.ReadJwtToken(token);
                    session.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                    session.Other = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTokenDoesNotComeFromAKnownDecoder()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class OwnHandler
            {
                public JwtSecurityToken ReadJwtToken(string token) => new();
            }

            class C
            {
                void M(HttpContext context, string token)
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(new OwnHandler().ReadJwtToken(token).Claims, "jwt"));
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(new JwtSecurityToken().Claims, "jwt"));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAClaimsLocalIsPassedByReferenceBeforeItIsUsed()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class Holder
            {
                public Holder(ref IEnumerable<Claim> claims) { }
            }

            class C
            {
                static void Mutate(ref IEnumerable<Claim> claims) { }

                void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    var parsed = handler.ReadJwtToken(token);
                    var viaCall = parsed.Claims;
                    Mutate(ref viaCall);
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(viaCall, "jwt"));

                    var viaConstructor = parsed.Claims;
                    _ = new Holder(ref viaConstructor);
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(viaConstructor, "jwt"));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAClaimsLocalComesFromAnOutArgumentOfAnotherMethod()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class C
            {
                static void Fill(out IEnumerable<Claim> claims) { claims = []; }

                void M(HttpContext context)
                {
                    Fill(out var claims);
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_AClaimReadByTryGetClaim()
    {
        return VerifyAsync("""
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.JsonWebTokens;
            class C
            {
                void M(HttpContext context, JsonWebTokenHandler handler, string token)
                {
                    var parsed = handler.ReadJsonWebToken(token);
                    if (parsed.TryGetClaim("sub", out var claim))
                    {
                        {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { claim }, "jwt"))|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAFieldIsCoalesceAssignedAPrincipal()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            static class Registry
            {
                public static void Add(Claim claim) { }
            }

            class C
            {
                ClaimsPrincipal? cached;

                void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    var parsed = handler.ReadJwtToken(token);
                    cached ??= new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                    Registry.Add(new Claim("sub", "value"));
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ATokenCapturedByTwoNestedLambdas()
    {
        return VerifyAsync("""
            using System;
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            class C
            {
                Action M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    var parsed = handler.ReadJwtToken(token);
                    return () =>
                    {
                        Action inner = () =>
                        {
                            {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                        };
                        inner();
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenANestedLambdaReadsTheValidationResultOfTheOuterBody()
    {
        return VerifyAsync("""
            using System;
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                async Task<Action> M(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    return () =>
                    {
                        Action inner = () =>
                        {
                            if (result.IsValid)
                            {
                                var parsed = handler.ReadJwtToken(token);
                                context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                            }
                        };
                        inner();
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheResultOfTheCheckIsNotFromAValidationCall()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            static class Checks
            {
                public static Task<TokenValidationResult> CheckAsync(string token) => Task.FromResult(new TokenValidationResult());
            }

            class C
            {
                Task<TokenValidationResult> Own(string token) => Task.FromResult(new TokenValidationResult());

                async Task FromOwnMethod(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    var result = await Own(token);
                    if (!result.IsValid)
                    {
                        return;
                    }

                    var parsed = handler.ReadJwtToken(token);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }

                async Task FromStaticHelper(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    var result = await Checks.CheckAsync(token).ConfigureAwait(false);
                    if (!result.IsValid)
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
    public Task Reports_WhenTheResultOfTheCheckComesFromAHandlerHelperOrANewResult()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class Handler : JwtSecurityTokenHandler
            {
                public Task<TokenValidationResult> Check(string token) => Task.FromResult(new TokenValidationResult());
            }

            class C
            {
                async Task FromHandlerMethod(HttpContext context, Handler handler, string token)
                {
                    var result = await handler.Check(token);
                    if (!result.IsValid)
                    {
                        return;
                    }

                    var parsed = handler.ReadJwtToken(token);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }

                void FromNewResult(HttpContext context, JwtSecurityTokenHandler handler, string token)
                {
                    var result = new TokenValidationResult();
                    if (!result.IsValid)
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
    public Task Reports_WhenTheValidationResultIsReassignedOrPassedByReference()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                static void Reset(ref TokenValidationResult result) { }

                async Task Reassigned(HttpContext context, JwtSecurityTokenHandler handler, string token, string other, TokenValidationParameters parameters)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    result = await handler.ValidateTokenAsync(other, parameters);
                    if (!result.IsValid)
                    {
                        return;
                    }

                    var parsed = handler.ReadJwtToken(token);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }

                async Task ByReference(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    Reset(ref result);
                    if (!result.IsValid)
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
    public Task Silent_WhenTheResultGuardTakesAnyRecognizedShape()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                async Task ComparedToTrue(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    if (result.IsValid == true)
                    {
                        var parsed = handler.ReadJwtToken(token);
                        context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                    }
                }

                async Task AndedWithAnotherTest(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters, bool flag)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    if (flag && result.IsValid)
                    {
                        var parsed = handler.ReadJwtToken(token);
                        context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                    }
                }

            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheResultGuardExitsOrBranchesOnTheFailedResult()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                async Task ComparedToFalse(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    if (result.IsValid == false)
                    {
                        throw new System.InvalidOperationException();
                    }

                    var parsed = handler.ReadJwtToken(token);
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                }

                async Task FailedBranchWithElse(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    if (!result.IsValid)
                    {
                        System.Console.WriteLine("invalid");
                    }
                    else
                    {
                        var parsed = handler.ReadJwtToken(token);
                        context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFailedResultGuardDoesNotExit()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                async Task EmptyBlock(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    if (!result.IsValid)
                    {
                    }

                    var parsed = handler.ReadJwtToken(token);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }

                async Task UnrelatedTest(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters, bool flag)
                {
                    var result = await handler.ValidateTokenAsync(token, parameters);
                    if (!flag)
                    {
                        return;
                    }

                    var parsed = handler.ReadJwtToken(token);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }

                async Task LoopContinue(HttpContext context, JwtSecurityTokenHandler handler, string[] tokens, TokenValidationParameters parameters)
                {
                    foreach (var token in tokens)
                    {
                        var result = await handler.ValidateTokenAsync(token, parameters);
                        if (!result.IsValid)
                        {
                            continue;
                        }

                        var parsed = handler.ReadJwtToken(token);
                        {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnIteratorLeavesWithYieldBreakAfterAFailedResult()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.JsonWebTokens;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                IEnumerable<int> M(HttpContext context, JsonWebTokenHandler handler, string token, TokenValidationParameters parameters)
                {
                    var result = handler.ValidateToken(token, parameters);
                    if (!result.IsValid)
                    {
                        yield break;
                    }

                    var parsed = handler.ReadJsonWebToken(token);
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                    yield return 1;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Classifies_TokensHeldInPropertiesByTheirStability()
    {
        return VerifyAsync("""
            using System.IdentityModel.Tokens.Jwt;
            using System.Security.Claims;
            using Microsoft.AspNetCore.Http;
            using Microsoft.IdentityModel.Tokens;
            class C
            {
                string Auto { get; set; } = string.Empty;

                string Expression => string.Empty;

                string Block
                {
                    get { return string.Empty; }
                }

                string this[int index] => string.Empty;

                void Stable(HttpContext context, JwtSecurityTokenHandler handler, TokenValidationParameters parameters)
                {
                    handler.ValidateToken(this.Auto, parameters, out _);
                    var parsed = handler.ReadJwtToken(Auto);
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                }

                void OtherInstance(HttpContext context, JwtSecurityTokenHandler handler, C other, TokenValidationParameters parameters)
                {
                    handler.ValidateToken(other.Auto, parameters, out _);
                    var parsed = handler.ReadJwtToken(Auto);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }

                void ExpressionBodied(HttpContext context, JwtSecurityTokenHandler handler, TokenValidationParameters parameters)
                {
                    handler.ValidateToken(Expression, parameters, out _);
                    var parsed = handler.ReadJwtToken(Expression);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }

                void BlockBodied(HttpContext context, JwtSecurityTokenHandler handler, TokenValidationParameters parameters)
                {
                    handler.ValidateToken(Block, parameters, out _);
                    var parsed = handler.ReadJwtToken(Block);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }

                void Indexed(HttpContext context, JwtSecurityTokenHandler handler, TokenValidationParameters parameters)
                {
                    handler.ValidateToken(this[0], parameters, out _);
                    var parsed = handler.ReadJwtToken(this[0]);
                    {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                }
            }
            """);
    }
}
