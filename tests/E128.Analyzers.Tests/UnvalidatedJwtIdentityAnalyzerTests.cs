using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed partial class UnvalidatedJwtIdentityAnalyzerTests
{
    private const string Stubs = """
                                 namespace Microsoft.IdentityModel.Tokens
                                 {
                                     public class SecurityToken { }
                                     public class TokenValidationParameters { }
                                     public class TokenValidationResult { public bool IsValid { get; set; } }
                                 }
                                 namespace System.IdentityModel.Tokens.Jwt
                                 {
                                     public class JwtSecurityToken : Microsoft.IdentityModel.Tokens.SecurityToken
                                     {
                                         public JwtSecurityToken() { }
                                         public JwtSecurityToken(string token) { }
                                         public System.Collections.Generic.IEnumerable<System.Security.Claims.Claim> Claims => [];
                                         public string Subject => string.Empty;
                                         public JwtPayload Payload => new();
                                     }
                                     public class JwtPayload
                                     {
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
                                         public virtual System.Threading.Tasks.Task<Microsoft.IdentityModel.Tokens.TokenValidationResult> ValidateTokenAsync(Microsoft.IdentityModel.Tokens.SecurityToken token, Microsoft.IdentityModel.Tokens.TokenValidationParameters validationParameters) => System.Threading.Tasks.Task.FromResult(new Microsoft.IdentityModel.Tokens.TokenValidationResult());
                                     }
                                 }
                                 namespace Microsoft.IdentityModel.JsonWebTokens
                                 {
                                     public class JsonWebToken : Microsoft.IdentityModel.Tokens.SecurityToken
                                     {
                                         public System.Collections.Generic.IEnumerable<System.Security.Claims.Claim> Claims => [];
                                         public System.Security.Claims.Claim GetClaim(string name) => new(name, string.Empty);
                                         public bool TryGetClaim(string name, out System.Security.Claims.Claim claim)
                                         {
                                             claim = GetClaim(name);
                                             return true;
                                         }
                                     }
                                     public class JsonWebTokenHandler
                                     {
                                         public virtual JsonWebToken ReadJsonWebToken(string token) => new();
                                         public virtual Microsoft.IdentityModel.Tokens.TokenValidationResult ValidateToken(string token, Microsoft.IdentityModel.Tokens.TokenValidationParameters validationParameters) => new();
                                         public virtual System.Threading.Tasks.Task<Microsoft.IdentityModel.Tokens.TokenValidationResult> ValidateTokenAsync(string token, Microsoft.IdentityModel.Tokens.TokenValidationParameters validationParameters) => System.Threading.Tasks.Task.FromResult(new Microsoft.IdentityModel.Tokens.TokenValidationResult());
                                     }
                                 }
                                 namespace Microsoft.AspNetCore.Http
                                 {
                                     public abstract class HttpContext
                                     {
                                         public abstract System.Security.Claims.ClaimsPrincipal User { get; set; }
                                     }
                                 }
                                 namespace Microsoft.AspNetCore.Authentication
                                 {
                                     public class AuthenticationTicket
                                     {
                                         public AuthenticationTicket(System.Security.Claims.ClaimsPrincipal principal, string authenticationScheme) { }
                                     }
                                 }
                                 """;

    private static Task VerifyAsync(string source)
    {
        return AnalyzerTestRunner.VerifyAsync<UnvalidatedJwtIdentityAnalyzer>(
            source,
            options: new AnalyzerTestOptions { Sources = [Stubs] });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task JwtClaimsAssignedToUser_Fires()
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
                                   var claims = parsed.Claims;
                                   var identity = new ClaimsIdentity(claims, "jwt");
                                   var principal = new ClaimsPrincipal(identity);
                                   {|E128202:context.User = principal|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task JsonWebTokenClaimsInTicket_Fires()
    {
        return VerifyAsync("""
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Authentication;
                           using Microsoft.IdentityModel.JsonWebTokens;
                           class C
                           {
                               AuthenticationTicket M(JsonWebTokenHandler handler, string token)
                               {
                                   return {|E128202:new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "jwt", claims: handler.ReadJsonWebToken(token).Claims)), "jwt")|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InspectionAndUnauthenticatedIdentity_Silent()
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
                                   var identity = new ClaimsIdentity(parsed.Claims);
                                   context.User = new ClaimsPrincipal(identity);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SynchronousValidationOfSameToken_Silent()
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
                                   handler.ValidateToken(token, parameters, out _);
                                   var parsed = handler.ReadJwtToken(token);
                                   context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SynchronousValidationOfDifferentToken_Fires()
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
                                   handler.ValidateToken(other, parameters, out _);
                                   var parsed = handler.ReadJwtToken(token);
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task UncheckedAsyncValidation_Fires()
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
                                   await handler.ValidateTokenAsync(token, parameters);
                                   var parsed = handler.ReadJwtToken(token);
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReassignedPrincipal_Silent()
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
                                   var identity = new ClaimsIdentity(parsed.Claims, "jwt");
                                   var principal = new ClaimsPrincipal(identity);
                                   principal = verified;
                                   context.User = principal;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReassignedTokenAfterValidation_Fires()
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
                                   handler.ValidateToken(token, parameters, out _);
                                   token = other;
                                   var parsed = handler.ReadJwtToken(token);
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BranchValidationDoesNotDominate_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           using Microsoft.IdentityModel.Tokens;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters, bool validate)
                               {
                                   if (validate) handler.ValidateToken(token, parameters, out _);
                                   var parsed = handler.ReadJwtToken(token);
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonConstantAuthenticationType_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token, string type)
                               {
                                   var parsed = handler.ReadJwtToken(token);
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, type))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddClaimsFromDecodedToken_Fires()
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
                                   {|E128202:context.User = new ClaimsPrincipal(identity)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CheckedIsValidGuard_Silent()
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
                                   if (!result.IsValid)
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
    public Task IsValidGuardOfDifferentToken_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using System.Threading.Tasks;
                           using Microsoft.AspNetCore.Http;
                           using Microsoft.IdentityModel.Tokens;
                           class C
                           {
                               async Task M(HttpContext context, JwtSecurityTokenHandler handler, string token, string other, TokenValidationParameters parameters)
                               {
                                   var result = await handler.ValidateTokenAsync(other, parameters);
                                   if (!result.IsValid) throw new System.Exception();
                                   var parsed = handler.ReadJwtToken(token);
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddClaimOfOneDecodedClaim_Fires()
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
                                   var parsed = handler.ReadJwtToken(token);
                                   var identity = new ClaimsIdentity("jwt");
                                   identity.AddClaim(parsed.Claims.First(c => c.Type == "sub"));
                                   {|E128202:context.User = new ClaimsPrincipal(identity)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HandBuiltClaimList_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Linq;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   var parsed = handler.ReadJwtToken(token);
                                   var claims = new List<Claim>();
                                   claims.AddRange(parsed.Claims.Where(c => c.Type == "role"));
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddClaimsAfterSink_Fires()
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
                                   {|E128202:context.User = new ClaimsPrincipal(identity)|};
                                   identity.AddClaims(handler.ReadJwtToken(token).Claims);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PositiveIsValidBranch_Silent()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using System.Threading.Tasks;
                           using Microsoft.AspNetCore.Http;
                           using Microsoft.IdentityModel.Tokens;
                           class C
                           {
                               async Task M(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters, bool enabled)
                               {
                                   var result = await handler.ValidateTokenAsync(token, parameters);
                                   if (enabled && result.IsValid)
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
    public Task FailedIsValidBranch_Fires()
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
                                   if (!result.IsValid)
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
    public Task LambdaAfterParentValidation_Silent()
    {
        return VerifyAsync("""
                           using System;
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           using Microsoft.IdentityModel.Tokens;
                           class C
                           {
                               Action M(HttpContext context, JwtSecurityTokenHandler handler, string token, TokenValidationParameters parameters)
                               {
                                   handler.ValidateToken(token, parameters, out _);
                                   var parsed = handler.ReadJwtToken(token);
                                   return () =>
                                   {
                                       context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                                   };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ValidatedClaimsWithAddedUnvalidatedToken_Fires()
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
                                   handler.ValidateToken(token, parameters, out _);
                                   var identity = new ClaimsIdentity(handler.ReadJwtToken(token).Claims, "jwt");
                                   identity.AddClaims(handler.ReadJwtToken(other).Claims);
                                   {|E128202:context.User = new ClaimsPrincipal(identity)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ValidatedAndUnvalidatedIdentities_Fires()
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
                                   handler.ValidateToken(token, parameters, out _);
                                   var first = new ClaimsIdentity(handler.ReadJwtToken(token).Claims, "jwt");
                                   var second = new ClaimsIdentity(handler.ReadJwtToken(other).Claims, "jwt");
                                   {|E128202:context.User = new ClaimsPrincipal(new[] { first, second })|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ForeachAddsDecodedClaimsToIdentity_Fires()
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
                                   foreach (var claim in parsed.Claims)
                                   {
                                       identity.AddClaim(claim);
                                   }
                                   {|E128202:context.User = new ClaimsPrincipal(identity)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ForeachCopiesDecodedClaimValuesToList_Fires()
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
                                   var parsed = handler.ReadJwtToken(token);
                                   var claims = new List<Claim>();
                                   foreach (var claim in parsed.Claims)
                                   {
                                       claims.Add(new Claim(claim.Type, claim.Value));
                                   }
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListForEachAddsDecodedClaimsToIdentity_Fires()
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
                                   var parsed = handler.ReadJwtToken(token);
                                   var identity = new ClaimsIdentity("jwt");
                                   parsed.Claims.ToList().ForEach(claim => identity.AddClaim(claim));
                                   {|E128202:context.User = new ClaimsPrincipal(identity)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task LocalFunctionCalledBeforeValidation_Fires()
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
                                   var parsed = handler.ReadJwtToken(token);
                                   SetUser();
                                   handler.ValidateToken(token, parameters, out _);
                                   void SetUser()
                                   {
                                       {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FilteredClaimsReassignedToSameLocal_Fires()
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
                                   var claims = handler.ReadJwtToken(token).Claims;
                                   claims = claims.Where(claim => claim.Type == "role");
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrincipalOverwrittenAfterSink_Fires()
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
                                   var principal = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                                   {|E128202:context.User = principal|};
                                   principal = verified;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddUnvalidatedIdentityToPrincipal_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   var identity = new ClaimsIdentity(handler.ReadJwtToken(token).Claims, "jwt");
                                   var principal = new ClaimsPrincipal();
                                   principal.AddIdentity(identity);
                                   {|E128202:context.User = principal|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PrincipalIdentityCollectionExpression_Fires()
    {
        return VerifyAsync("""
                           using System.IdentityModel.Tokens.Jwt;
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context, JwtSecurityTokenHandler handler, string token)
                               {
                                   var identity = new ClaimsIdentity(handler.ReadJwtToken(token).Claims, "jwt");
                                   {|E128202:context.User = new ClaimsPrincipal([identity])|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConditionalAccessToDecodedClaims_Fires()
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
                                   var claims = parsed?.Claims ?? [];
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CapturedValidationResultGuardInLambda_Silent()
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
                                       if (result.IsValid)
                                       {
                                           var parsed = handler.ReadJwtToken(token);
                                           context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"));
                                       }
                                   };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DeconstructedTokenAfterValidation_Fires()
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
                                   handler.ValidateToken(token, parameters, out _);
                                   (token, other) = (other, token);
                                   var parsed = handler.ReadJwtToken(token);
                                   {|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(parsed.Claims, "jwt"))|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FreshClaimsAddedToAuthenticatedIdentity_Silent()
    {
        return VerifyAsync("""
                           using System.Security.Claims;
                           using Microsoft.AspNetCore.Http;
                           class C
                           {
                               void M(HttpContext context)
                               {
                                   var identity = new ClaimsIdentity("jwt");
                                   identity.AddClaim(new Claim("role", "reader"));
                                   context.User = new ClaimsPrincipal(identity);
                               }
                           }
                           """);
    }
}
