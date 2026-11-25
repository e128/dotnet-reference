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
}
