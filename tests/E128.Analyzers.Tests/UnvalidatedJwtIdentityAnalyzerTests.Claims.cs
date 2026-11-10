using System;
using System.Threading.Tasks;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed partial class UnvalidatedJwtIdentityAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task DecodedClaimsAddedToUnauthenticatedIdentity_Silent()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   var identity = new ClaimsIdentity();
                                   identity.AddClaims(handler.ReadJwtToken(token).Claims);
                                   context.User = new ClaimsPrincipal(identity);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TokenStringConstructorClaims_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, string token)
                               {
                                   var parsed = new JwtSecurityToken(token);
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReadTokenClaims_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   var parsed = (JwtSecurityToken)handler.ReadToken(token);
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CheckedAsyncValidationOfDecodedToken_Silent()
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
                                   var parsed = handler.ReadJwtToken(token);
                                   var result = await handler.ValidateTokenAsync(parsed, parameters);
                                   if (result.IsValid)
                                   {
                                       context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LambdaUsingCapturedDecodedToken_Fires()
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
                                       {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                                   };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReadTokenDeclarationPattern_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   if (handler.ReadToken(token) is JwtSecurityToken jwt)
                                   {
                                       {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "jwt"))|};
                                   }
                               }
                           }
                           """);
    }

    [Theory]
    [InlineData("if (!result.IsValid) return;")]
    [InlineData("if (result.IsValid)")]
    [Trait("Category", "CI")]
    public Task TokenRewrittenBeforeAsyncValidationGuard_Fires(string guard)
    {
        return VerifyAsync($$"""
                             using System.IdentityModel.Tokens.Jwt;
                             using System.Security.Claims;
                             using System.Threading.Tasks;
                             using Microsoft.AspNetCore.Http;
                             using Microsoft.IdentityModel.Tokens;
                             class C
                             {
                                 async Task M(HttpContext context, JwtSecurityTokenHandler handler, string token, string other, TokenValidationParameters parameters)
                                 {
                                     var result = await handler.ValidateTokenAsync(token, parameters);
                                     token = other;
                                     {{guard}}
                                     {
                                         var parsed = handler.ReadJwtToken(token);
                                         {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                                     }
                                 }
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CapturedTokenRewrittenAfterLambdaCreation_Fires()
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
                                   Action setUser = () =>
                                   {
                                       var parsed = handler.ReadJwtToken(token);
                                       {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                                   };
                                   token = other;
                                   return setUser;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CapturedClaimsUpdatedAfterLambdaCreation_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Collections.Generic;
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               Action M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   IEnumerable<Claim> claims = [];
                                   Action setUser = () =>
                                   {
                                       {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"))|};
                                   };
                                   claims = handler.ReadJwtToken(token).Claims;
                                   return setUser;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalFunctionCalledBeforeCapturedClaimsReset_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   var claims = handler.ReadJwtToken(token).Claims;
                                   SetUser();
                                   claims = [];
                                   void SetUser()
                                   {
                                       {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"))|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CheckedConfiguredAsyncValidation_Silent()
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
                                   var result = await handler.ValidateTokenAsync(token, parameters).ConfigureAwait(false);
                                   if (!result.IsValid) return;
                                   var parsed = handler.ReadJwtToken(token);
                                   context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SynchronousValidationInTryWithReturningCatch_Silent()
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
                                   try
                                   {
                                       handler.ValidateToken(token, parameters, out _);
                                   }
                                   catch (Exception)
                                   {
                                       return;
                                   }
                                   var parsed = handler.ReadJwtToken(token);
                                   context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SynchronousValidationInTryWithContinuingCatch_Fires()
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
                                   try
                                   {
                                       handler.ValidateToken(token, parameters, out _);
                                   }
                                   catch (Exception)
                                   {
                                   }
                                   var parsed = handler.ReadJwtToken(token);
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConditionalPrincipalWithDecodedClaims_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token, ClaimsPrincipal verified, bool useVerified)
                               {
                                   var parsed = handler.ReadJwtToken(token);
                                   {|E128202:context.User = useVerified ? verified : new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CoalescedPrincipalWithDecodedClaims_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token, ClaimsPrincipal verified)
                               {
                                   var parsed = handler.ReadJwtToken(token);
                                   {|E128202:context.User = verified ?? new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConditionalPrincipalGuardedByIsValid_Silent()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using System.Threading.Tasks;
                           using Microsoft.AspNetCore.Http;
                           using Microsoft.IdentityModel.Tokens;
                           class C
                           {
                               async Task M(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters, ClaimsPrincipal verified)
                               {
                                   var result = await handler.ValidateTokenAsync(token, parameters);
                                   var parsed = handler.ReadJwtToken(token);
                                   context.User = result.IsValid ? new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt")) : verified;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DecodedClaimsInIdentityWithAuthenticatedBase_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   var baseIdentity = new ClaimsIdentity("jwt");
                                   var parsed = handler.ReadJwtToken(token);
                                   var identity = new ClaimsIdentity(baseIdentity, parsed.Claims);
                                   {|E128202:context.User = new ClaimsPrincipal(identity)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DecodedClaimsAddedToAuthenticatedGenericIdentity_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using System.Security.Principal;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   var identity = new GenericIdentity("user", "jwt");
                                   identity.AddClaims(handler.ReadJwtToken(token).Claims);
                                   {|E128202:context.User = new ClaimsPrincipal(identity)|};
                               }
                           }
                           """);
    }

    [Theory]
    [InlineData("ref")]
    [InlineData("out")]
    [Trait("Category", "CI")]
    public Task OpaqueWriteAfterSink_Fires(string modifier)
    {
        return VerifyAsync($$"""
                             using System.IdentityModel.Tokens.Jwt;
                             using System.Security.Claims;
                             using Microsoft.AspNetCore.Http;
                             class C
                             {
                                 void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                                 {
                                     var parsed = handler.ReadJwtToken(token);
                                     var principal = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                                     {|E128202:context.User = principal|};
                                     Overwrite({{modifier}} principal);
                                 }
                                 static void Overwrite({{modifier}} ClaimsPrincipal principal)
                                 {
                                     principal = new ClaimsPrincipal();
                                 }
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListForEachAddsLocalCopyOfDecodedClaim_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Linq;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   var identity = new ClaimsIdentity("jwt");
                                   handler.ReadJwtToken(token).Claims.ToList().ForEach(claim =>
                                   {
                                       var copy = claim;
                                       identity.AddClaim(copy);
                                   });
                                   {|E128202:context.User = new ClaimsPrincipal(identity)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ClaimsAddedToListAfterIdentityConstruction_Silent()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   var claims = new List<Claim>();
                                   var identity = new ClaimsIdentity(claims, "jwt");
                                   claims.AddRange(handler.ReadJwtToken(token).Claims);
                                   context.User = new ClaimsPrincipal(identity);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IdentityReassignedAfterAddingDecodedClaims_Silent()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   var identity = new ClaimsIdentity("jwt");
                                   identity.AddClaims(handler.ReadJwtToken(token).Claims);
                                   identity = new ClaimsIdentity("jwt");
                                   context.User = new ClaimsPrincipal(identity);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NestedGotoSkipsValidation_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           using Microsoft.IdentityModel.Tokens;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters, bool skip)
                               {
                                   if (skip)
                                   {
                                       goto AssignUser;
                                   }
                                   handler.ValidateToken(token, parameters, out _);
                               AssignUser:
                                   var parsed = handler.ReadJwtToken(token);
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConditionalCoalesceAssignmentSkipsValidation_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           using Microsoft.IdentityModel.Tokens;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters, ClaimsPrincipal verified)
                               {
                                   verified ??= handler.ValidateToken(token, parameters, out _);
                                   var parsed = handler.ReadJwtToken(token);
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShortCircuitReassignmentDoesNotHideDecodedClaims_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Linq;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token, IEnumerable<Claim> safe, bool enabled)
                               {
                                   var claims = handler.ReadJwtToken(token).Claims;
                                   if (enabled && (claims = safe).Any()) return;
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SubjectCopiedToClaim_Fires()
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
                                   var identity = new ClaimsIdentity("jwt");
                                   identity.AddClaim(new Claim("sub", parsed.Subject));
                                   {|E128202:context.User = new ClaimsPrincipal(identity)|};
                               }
                           }
                           """);
    }

    [Theory]
    [InlineData("var parsed = handler.ReadJwtToken(token); var claims = parsed.Payload.Claims; {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, \"jwt\"))|};")]
    [InlineData("var parsed = handler.ReadJwtToken(token); var payload = parsed.Payload; {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(payload.Claims, \"jwt\"))|};")]
    [InlineData("var claim = json.ReadJsonWebToken(token).GetClaim(\"sub\"); {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity([claim], \"jwt\"))|};")]
    [InlineData("if (json.ReadJsonWebToken(token).TryGetClaim(\"sub\", out var claim)) { {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity([claim], \"jwt\"))|}; }")]
    [InlineData("Claim claim; json.ReadJsonWebToken(token).TryGetClaim(\"sub\", out claim); {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity([claim], \"jwt\"))|};")]
    [InlineData("IEnumerable<Claim> claims = null; claims ??= handler.ReadJwtToken(token).Claims; {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, \"jwt\"))|};")]
    [InlineData("IEnumerable<Claim> claims = []; Action populate = () => claims = handler.ReadJwtToken(token).Claims; populate(); {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, \"jwt\"))|};")]
    [InlineData("var parsed = new JwtSecurityToken(); parsed = handler.ReadJwtToken(token); {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, \"jwt\"))|}; Refresh(ref parsed);")]
    [InlineData("switch (handler.ReadToken(token)) { case JwtSecurityToken jwt: {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, \"jwt\"))|}; break; }")]
    [Trait("Category", "CI")]
    public Task AdditionalLocalSources_Fires(string body)
    {
        return VerifyAsync($$"""
                             using System;
                             using System.Collections.Generic;
                             using System.IdentityModel.Tokens.Jwt;
                             using System.Security.Claims;
                             using Microsoft.AspNetCore.Http;
                             using Microsoft.IdentityModel.JsonWebTokens;
                             class C
                             {
                                 void M(HttpContext context, JwtSecurityTokenHandler handler, JsonWebTokenHandler json, string token)
                                 {
                                     {{body}}
                                 }
                                 static void Refresh(ref JwtSecurityToken token) { token = new JwtSecurityToken(); }
                             }
                             """);
    }

    [Theory]
    [InlineData("request.Token", "request.Token", "", false)]
    [InlineData("request.Field", "request.Field", "", false)]
    [InlineData("request.Token", "other.Token", "", true)]
    [InlineData("request.Field", "other.Field", "", true)]
    [InlineData("request.Token", "request.Token", "request.Token = other.Token;", true)]
    [InlineData("request.Field", "request.Field", "request.Field = other.Field;", true)]
    [InlineData("request.Token", "request.Token", "request = other;", true)]
    [InlineData("request.VolatileToken", "request.VolatileToken", "", true)]
    [Trait("Category", "CI")]
    public Task MemberValidationRespectsStorage(string validated, string decoded, string mutation, bool fires)
    {
        var sink = $"context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken({decoded}).Claims, \"jwt\"))";
        return VerifyAsync($$"""
                             using System.IdentityModel.Tokens.Jwt;
                             using System.Security.Claims;
                             using Microsoft.AspNetCore.Http;
                             using Microsoft.IdentityModel.Tokens;
                             class Request
                             {
                                 public string Token { get; set; }
                                 public string Field;
                                 public string VolatileToken => System.Guid.NewGuid().ToString();
                             }
                             class C
                             {
                                 void M(HttpContext context, JwtSecurityTokenHandler handler, Request request, Request other, TokenValidationParameters parameters)
                                 {
                                     handler.ValidateToken({{validated}}, parameters, out _);
                                     {{mutation}}
                                     {{(fires ? "{|E128202:" + sink + "|}" : sink)}};
                                 }
                             }
                             """);
    }

    [Theory]
    [InlineData("if (!result.IsValid) return;", "", false)]
    [InlineData("if (result.IsValid)", "", false)]
    [InlineData("", "", true)]
    [InlineData("if (!result.IsValid) return;", "token = other;", true)]
    [InlineData("if (result.IsValid)", "token = other;", true)]
    [Trait("Category", "CI")]
    public Task JsonSynchronousValidationRequiresUnchangedCheckedResult(string guard, string mutation, bool fires)
    {
        const string sink = "context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJsonWebToken(token).Claims, \"jwt\"))";
        return VerifyAsync($$"""
                             using System.Security.Claims;
                             using Microsoft.AspNetCore.Http;
                             using Microsoft.IdentityModel.JsonWebTokens;
                             using Microsoft.IdentityModel.Tokens;
                             class C
                             {
                                 void M(HttpContext context, JsonWebTokenHandler handler, string token, string other, TokenValidationParameters parameters)
                                 {
                                     var result = handler.ValidateToken(token, parameters);
                                     {{mutation}}
                                     {{guard}}
                                     {
                                         {{(fires ? "{|E128202:" + sink + "|}" : sink)}};
                                     }
                                 }
                             }
                             """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SwitchExpressionPatternSource_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   var claims = handler.ReadToken(token) switch
                                   {
                                       JwtSecurityToken jwt => jwt.Claims,
                                       _ => System.Linq.Enumerable.Empty<Claim>()
                                   };
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"))|};
                               }
                           }
                           """);
    }

    private const string LoopSink = "context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, \"jwt\"))";
    private const string LoopHit = "{|E128202:" + LoopSink + "|}";
    private const string InlineSink = "context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\"))";

    [Theory]
    [InlineData("while (flag) { " + LoopHit + "; claims = handler.ReadJwtToken(token).Claims; }")]
    [InlineData("foreach (var item in items) { " + LoopHit + "; claims = handler.ReadJwtToken(item).Claims; }")]
    [InlineData("do { " + LoopHit + "; claims = handler.ReadJwtToken(token).Claims; } while (flag);")]
    [InlineData("while (flag) { claims = []; " + LoopSink + "; claims = handler.ReadJwtToken(token).Claims; }")]
    [InlineData("while (flag) { " + LoopSink + "; } claims = handler.ReadJwtToken(token).Claims;")]
    [InlineData("while (flag) { " + LoopSink + "; claims = handler.ReadJwtToken(token).Claims; claims = []; }")]
    [InlineData("for (; flag; claims = handler.ReadJwtToken(token).Claims) { claims = []; " + LoopSink + "; }")]
    [InlineData("foreach (var item in items) { IEnumerable<Claim> local; if (flag) { local = []; } else { local = claims; } context.User = new ClaimsPrincipal(new ClaimsIdentity(local, \"jwt\")); local = handler.ReadJwtToken(item).Claims; }")]
    [InlineData("handler.ValidateToken(token, parameters, out _); while (flag) { " + LoopHit + "; token = next; claims = handler.ReadJwtToken(token).Claims; }")]
    [InlineData("handler.ValidateToken(token, parameters, out _); while (flag) { {|E128202:" + InlineSink + "|}; token = next; }")]
    [InlineData("handler.ValidateToken(token, parameters, out _); while (flag) { " + InlineSink + "; }")]
    [InlineData("while (flag) { " + LoopSink + "; handler.ValidateToken(token, parameters, out _); claims = handler.ReadJwtToken(token).Claims; }")]
    [Trait("Category", "CI")]
    public Task LoopBackEdgeDefinitions(string body)
    {
        return VerifyAsync($$"""
                             using System.Collections.Generic;
                             using System.IdentityModel.Tokens.Jwt;
                             using System.Security.Claims;
                             using Microsoft.AspNetCore.Http;
                             using Microsoft.IdentityModel.Tokens;
                             class C
                             {
                                 void M(HttpContext context, JwtSecurityTokenHandler handler, TokenValidationParameters parameters, string token, string next, string[] items, bool flag)
                                 {
                                     IEnumerable<Claim> claims = [];
                                     {{body}}
                                 }
                             }
                             """);
    }

    [Theory]
    [InlineData("Run(); void Run() { context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\")); }", true)]
    [InlineData("Action run = Run; void Run() { context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\")); }", true)]
    [InlineData("Outer(); void Outer() { Run(); } void Run() { context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\")); }", true)]
    [InlineData("void Run() { context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\")); }", false)]
    [InlineData("void Outer() { Run(); } void Run() { context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\")); }", false)]
    [Trait("Category", "CI")]
    public Task LocalFunctionBodiesReportOnlyWhenReachable(string body, bool fires)
    {
        ArgumentNullException.ThrowIfNull(body);

        body = fires
            ? body.Replace("context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\"))", "{|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\"))|}", StringComparison.Ordinal)
            : body;
        return VerifyAsync($$"""
                             using System;
                             using System.IdentityModel.Tokens.Jwt;
                             using System.Security.Claims;
                             using Microsoft.AspNetCore.Http;
                             class C
                             {
                                 void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                                 {
                                     {{body}}
                                 }
                             }
                             """);
    }
}
