using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MiddlewareNextDelegateAnalyzerTests
{
    private static readonly AnalyzerTestOptions Options = new() { Sources = [TestStubs.AspNetCoreMinimal] };

    private static DiagnosticResult Expected()
    {
        return new DiagnosticResult("E128229", DiagnosticSeverity.Warning).WithLocation(0);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLambdaTakesAFuncTaskNextDelegate()
    {
        return AnalyzerTestRunner.VerifyAsync<MiddlewareNextDelegateAnalyzer>(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    app.Use(async (HttpContext context, {|#0:Func<Task>|} next) =>
                    {
                        await next();
                    });
                }
            }
            """,
            Expected(),
            Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheLambdaReturnsTheNextCall()
    {
        return AnalyzerTestRunner.VerifyAsync<MiddlewareNextDelegateAnalyzer>(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    app.Use((HttpContext context, {|#0:Func<Task>|} next) => next());
                }
            }
            """,
            Expected(),
            Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLambdaTakesARequestDelegate()
    {
        return AnalyzerTestRunner.VerifyAsync<MiddlewareNextDelegateAnalyzer>(
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
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheNextDelegateEscapesTheLambda()
    {
        return AnalyzerTestRunner.VerifyAsync<MiddlewareNextDelegateAnalyzer>(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    app.Use((HttpContext context, Func<Task> next) => Helper(context, next));
                }

                static Task Helper(HttpContext context, Func<Task> next) => next();
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheContextParameterIsReassigned()
    {
        return AnalyzerTestRunner.VerifyAsync<MiddlewareNextDelegateAnalyzer>(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    app.Use(async (HttpContext context, Func<Task> next) =>
                    {
                        context = new HttpContext();
                        await next();
                    });
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheUseMethodIsNotOnApplicationBuilder()
    {
        return AnalyzerTestRunner.VerifyAsync<MiddlewareNextDelegateAnalyzer>(
            """
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;

            class App
            {
                public void Use(Func<HttpContext, Func<Task>, Task> middleware)
                {
                }
            }

            class C
            {
                void M(App app)
                {
                    app.Use(async (HttpContext context, Func<Task> next) =>
                    {
                        await next();
                    });
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAspNetCoreIsNotReferenced()
    {
        return AnalyzerTestRunner.VerifyAsync<MiddlewareNextDelegateAnalyzer>(
            """
            using System;
            using System.Threading.Tasks;

            class C
            {
                void M()
                {
                    Func<object, Func<Task>, Task> middleware = (context, next) => next();
                }
            }
            """);
    }
}
