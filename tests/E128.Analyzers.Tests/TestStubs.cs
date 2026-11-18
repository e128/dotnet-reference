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
}
