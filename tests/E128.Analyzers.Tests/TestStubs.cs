namespace E128.Analyzers.Tests;

/// <summary>
///     Stub sources shared by paired analyzer and code fix test files. One copy
///     per stub keeps the fake package shapes in one place.
/// </summary>
internal static class TestStubs
{
    /// <summary>
    ///     Synthetic AngleSharp.Dom shape used by the text guard and its fixer tests.
    /// </summary>
    public const string AngleSharpDom =
        """
        namespace AngleSharp.Dom
        {
            public interface INode { string TextContent { get; } }
            public interface IElement : INode { bool IsVisible { get; } }
        }
        """;

    /// <summary>
    ///     Synthetic System.Numerics.Tensors shape used by the span-sum tests.
    /// </summary>
    public const string TensorPrimitives =
        """
        namespace System.Numerics.Tensors
        {
            public static class TensorPrimitives
            {
                public static float Sum(ReadOnlySpan<float> values)
                {
                    return 0f;
                }

                public static double Sum(ReadOnlySpan<double> values)
                {
                    return 0d;
                }

                public static float Max(ReadOnlySpan<float> values)
                {
                    return 0f;
                }

                public static double Max(ReadOnlySpan<double> values)
                {
                    return 0d;
                }

                public static float Min(ReadOnlySpan<float> values)
                {
                    return 0f;
                }

                public static double Min(ReadOnlySpan<double> values)
                {
                    return 0d;
                }

                public static void Add(
                    ReadOnlySpan<float> x,
                    ReadOnlySpan<float> y,
                    Span<float> destination)
                {
                }

                public static void Add(
                    ReadOnlySpan<double> x,
                    ReadOnlySpan<double> y,
                    Span<double> destination)
                {
                }

                public static void Multiply(
                    ReadOnlySpan<float> x,
                    ReadOnlySpan<float> y,
                    Span<float> destination)
                {
                }

                public static void Multiply(
                    ReadOnlySpan<double> x,
                    ReadOnlySpan<double> y,
                    Span<double> destination)
                {
                }
            }
        }
        """;

    /// <summary>
    ///     Synthetic minimal API surface used by the middleware and static-file tests.
    /// </summary>
    public const string AspNetCoreMinimal =
        """
        namespace Microsoft.AspNetCore.Http
        {
            public class HttpContext
            {
            }

            public delegate System.Threading.Tasks.Task RequestDelegate(HttpContext context);
        }

        namespace Microsoft.AspNetCore.Builder
        {
            public interface IApplicationBuilder
            {
            }

            public interface IEndpointRouteBuilder
            {
            }

            public static class UseMiddlewareExtensions
            {
                public static IApplicationBuilder Use(
                    this IApplicationBuilder app,
                    System.Func<Microsoft.AspNetCore.Http.HttpContext, System.Func<System.Threading.Tasks.Task>, System.Threading.Tasks.Task> middleware)
                {
                    return app;
                }

                public static IApplicationBuilder Use(
                    this IApplicationBuilder app,
                    System.Func<Microsoft.AspNetCore.Http.HttpContext, Microsoft.AspNetCore.Http.RequestDelegate, System.Threading.Tasks.Task> middleware)
                {
                    return app;
                }
            }

            public sealed class StaticFileOptions
            {
            }

            public static class StaticFileExtensions
            {
                public static IApplicationBuilder UseStaticFiles(this IApplicationBuilder app)
                {
                    return app;
                }

                public static IApplicationBuilder UseStaticFiles(this IApplicationBuilder app, StaticFileOptions options)
                {
                    return app;
                }
            }

            public static class StaticAssetsEndpointRouteBuilderExtensions
            {
                public static IEndpointRouteBuilder MapStaticAssets(this IEndpointRouteBuilder endpoints)
                {
                    return endpoints;
                }
            }
        }
        """;

    /// <summary>
    ///     Synthetic ASP.NET Core CORS builder used by the wildcard-with-credentials tests.
    /// </summary>
    public const string AspNetCoreCors =
        """
        namespace Microsoft.AspNetCore.Cors.Infrastructure
        {
            public sealed class CorsPolicyBuilder
            {
                public CorsPolicyBuilder AllowAnyOrigin()
                {
                    return this;
                }

                public CorsPolicyBuilder AllowCredentials()
                {
                    return this;
                }

                public CorsPolicyBuilder WithOrigins(params string[] origins)
                {
                    return this;
                }
            }
        }
        """;

    /// <summary>
    ///     Synthetic ASP.NET Core MVC attributes used by the antiforgery tests.
    /// </summary>
    public const string AspNetCoreMvc =
        """
        namespace Microsoft.AspNetCore.Mvc
        {
            public sealed class HttpGetAttribute : System.Attribute
            {
            }

            public sealed class HttpPostAttribute : System.Attribute
            {
            }

            public sealed class HttpPutAttribute : System.Attribute
            {
            }

            public sealed class HttpPatchAttribute : System.Attribute
            {
            }

            public sealed class HttpDeleteAttribute : System.Attribute
            {
            }

            public sealed class ValidateAntiForgeryTokenAttribute : System.Attribute
            {
            }

            public sealed class AutoValidateAntiforgeryTokenAttribute : System.Attribute
            {
            }

            public sealed class IgnoreAntiforgeryTokenAttribute : System.Attribute
            {
            }

            public sealed class ApiControllerAttribute : System.Attribute
            {
            }
        }
        """;

    /// <summary>
    ///     Synthetic Microsoft.IdentityModel.Tokens surface used by the unvalidated
    ///     JWT identity tests.
    /// </summary>
    public const string UnvalidatedJwtTokenBase =
        """
        namespace Microsoft.IdentityModel.Tokens
        {
            public class SecurityToken { }
            public class TokenValidationParameters { }
            public class TokenValidationResult { public bool IsValid { get; set; } }
        }
        """;

    /// <summary>
    ///     Synthetic System.IdentityModel.Tokens.Jwt surface used by the unvalidated
    ///     JWT identity tests.
    /// </summary>
    public const string UnvalidatedJwtSecurityToken =
        """
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

    /// <summary>
    ///     Synthetic Microsoft.IdentityModel.JsonWebTokens surface used by the
    ///     unvalidated JWT identity tests.
    /// </summary>
    public const string UnvalidatedJsonWebToken =
        """
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
            }
        }
        """;

    /// <summary>
    ///     Synthetic authenticated HttpContext used by the unvalidated JWT identity tests.
    /// </summary>
    public const string UnvalidatedJwtHttpContext =
        """
        namespace Microsoft.AspNetCore.Http
        {
            public abstract class HttpContext
            {
                public abstract System.Security.Claims.ClaimsPrincipal User { get; set; }
            }
        }
        """;

    /// <summary>
    ///     Synthetic authentication ticket used by the unvalidated JWT identity tests.
    /// </summary>
    public const string UnvalidatedJwtAuthenticationTicket =
        """
        namespace Microsoft.AspNetCore.Authentication
        {
            public class AuthenticationTicket
            {
                public AuthenticationTicket(System.Security.Claims.ClaimsPrincipal principal, string authenticationScheme) { }
            }
        }
        """;

    /// <summary>
    ///     The full unvalidated JWT identity stub set in one source file.
    /// </summary>
    public const string UnvalidatedJwtStubs =
        UnvalidatedJwtTokenBase + "\n"
        + UnvalidatedJwtSecurityToken + "\n"
        + UnvalidatedJsonWebToken + "\n"
        + UnvalidatedJwtHttpContext + "\n"
        + UnvalidatedJwtAuthenticationTicket;

    /// <summary>
    ///     Synthetic EF Core query surface used by the EfQueryInLoop tests. The test
    ///     project carries no EF Core package, so the snippets compile against this
    ///     DbSet shape and the ToListAsync extension.
    /// </summary>
    public const string EfQueryInLoopStub =
        """
        using System;
        using System.Collections;
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading.Tasks;
        using Microsoft.EntityFrameworkCore;

        namespace Microsoft.EntityFrameworkCore
        {
            public abstract class DbSet<T> : IQueryable<T>
            {
                public Type ElementType => typeof(T);
                public System.Linq.Expressions.Expression Expression => throw new NotImplementedException();
                public IQueryProvider Provider => throw new NotImplementedException();
                public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public static class EntityFrameworkQueryableExtensions
            {
                public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source)
                    => throw new NotImplementedException();
            }
        }
        """;

    /// <summary>
    ///     Synthetic EF Core context surface used by the SaveChangesInLoop tests.
    /// </summary>
    public const string SaveChangesInLoopEfStub =
        """
        namespace Microsoft.EntityFrameworkCore
        {
            public class DbContext
            {
                public virtual int SaveChanges() => 0;

                public virtual System.Threading.Tasks.Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
                    => System.Threading.Tasks.Task.FromResult(0);
            }

            public class DbSet<T>
            {
            }
        }
        """;

    /// <summary>
    ///     Synthetic EF Core extension surface used by the SaveChangesInLoop tests.
    /// </summary>
    public const string SaveChangesInLoopEfExtensionStub =
        """
        namespace Microsoft.EntityFrameworkCore
        {
            public static class EntityFrameworkQueryableExtensions
            {
                public static int SaveChanges<T>(this System.Collections.Generic.IEnumerable<T> source) => 0;
            }
        }
        """;

    /// <summary>
    ///     Synthetic DbConnection used as the prefix source of the AsyncDbIo tests.
    /// </summary>
    public const string AsyncDbIoFakeConnection =
        """
        using System;
        using System.Data;
        using System.Data.Common;

        sealed class FakeConnection : DbConnection
        {
            public override string ConnectionString { get; set; } = string.Empty;
            public override string Database => string.Empty;
            public override string DataSource => string.Empty;
            public override string ServerVersion => string.Empty;
            public override ConnectionState State => ConnectionState.Closed;
            public override void ChangeDatabase(string databaseName) { }
            public override void Close() { }
            public override void Open() { }
            public void Ping() { }
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
            protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
        }
        """;

    /// <summary>
    ///     The AsyncDbIo connection stub with the Tasks using, for sources that await.
    /// </summary>
    public const string AsyncDbIoFakeConnectionWithTaskUsing =
        """
        using System;
        using System.Data;
        using System.Data.Common;
        using System.Threading.Tasks;

        sealed class FakeConnection : DbConnection
        {
            public override string ConnectionString { get; set; } = string.Empty;
            public override string Database => string.Empty;
            public override string DataSource => string.Empty;
            public override string ServerVersion => string.Empty;
            public override ConnectionState State => ConnectionState.Closed;
            public override void ChangeDatabase(string databaseName) { }
            public override void Close() { }
            public override void Open() { }
            public void Ping() { }
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
            protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
        }
        """;
}
