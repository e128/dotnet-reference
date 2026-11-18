using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MiddlewareNextDelegateCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<MiddlewareNextDelegateAnalyzer, MiddlewareNextDelegateCodeFixProvider>(
            source,
            fixedCode,
            options: new CodeFixTestOptions { Sources = [TestStubs.AspNetCoreMinimal] });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ChangesTheParameterTypeAndPassesTheContext()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    app.Use(async (HttpContext context, {|E128229:Func<Task>|} next) =>
                    {
                        await next();
                    });
                }
            }
            """,
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    app.Use(async (HttpContext context, RequestDelegate next) =>
                    {
                        await next(context);
                    });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AddsTheHttpNamespace_WhenTheFileOmitsIt()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    app.Use(async (Microsoft.AspNetCore.Http.HttpContext context, {|E128229:Func<Task>|} next) =>
                    {
                        await next();
                    });
                }
            }
            """,
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    app.Use(async (Microsoft.AspNetCore.Http.HttpContext context, RequestDelegate next) =>
                    {
                        await next(context);
                    });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_KeepsEveryNextCallInTheBody()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    app.Use(async (HttpContext context, {|E128229:Func<Task>|} next) =>
                    {
                        await next();
                        await next();
                    });
                }
            }
            """,
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    app.Use(async (HttpContext context, RequestDelegate next) =>
                    {
                        await next(context);
                        await next(context);
                    });
                }
            }
            """);
    }
}
