using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnvalidatedJwtIdentityAnalyzerTests
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
        var test = new CSharpAnalyzerTest<UnvalidatedJwtIdentityAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.TestState.Sources.Add(Stubs);
        return test.RunAsync();
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
        body = fires
            ? body.Replace("context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\"))", "{|E128202:context.User = new ClaimsPrincipal(new ClaimsIdentity(handler.ReadJwtToken(token).Claims, \"jwt\"))|}", System.StringComparison.Ordinal)
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
