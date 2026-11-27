using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpContextAccessorInServiceAnalyzerTests
{
    private const string Stubs = """
        namespace Microsoft.AspNetCore.Http
        {
            public interface IHttpContextAccessor { }
            public sealed class HttpContext { }
            public interface IMiddleware { }
            public interface IEndpointFilter { }
        }

        namespace Microsoft.Extensions.Hosting
        {
            public interface IHostedService { }
        }

        namespace Microsoft.AspNetCore.Components
        {
            public abstract class ComponentBase { }
        }

        namespace Microsoft.AspNetCore.Mvc.Filters
        {
            public interface IActionFilter { }
        }

        namespace Microsoft.AspNetCore.SignalR
        {
            public abstract class Hub { }
        }

        namespace Microsoft.AspNetCore.Authorization
        {
            public abstract class AuthorizationHandler<T> where T : class { }
        }

        """;

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAPlainServiceInjectsTheAccessor()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            sealed class MyService
            {
                public MyService(Microsoft.AspNetCore.Http.IHttpContextAccessor [|accessor|]) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAPrimaryConstructorInjectsTheAccessor()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            sealed class MyService(Microsoft.AspNetCore.Http.IHttpContextAccessor [|accessor|]) { }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAPositionalRecordInjectsTheAccessor()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            sealed record MyService(Microsoft.AspNetCore.Http.IHttpContextAccessor [|accessor|]);
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsAMiddleware()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            sealed class MyMiddleware : Microsoft.AspNetCore.Http.IMiddleware
            {
                public MyMiddleware(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsAHostedService()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            sealed class MyWorker : Microsoft.Extensions.Hosting.IHostedService
            {
                public MyWorker(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeHasAConventionalInvoke()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            sealed class MyMiddleware
            {
                public MyMiddleware(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) { }

                public System.Threading.Tasks.Task Invoke(Microsoft.AspNetCore.Http.HttpContext context) => null;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsAComponent()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            sealed class MyComponent : Microsoft.AspNetCore.Components.ComponentBase
            {
                public MyComponent(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsAnMvcFilter()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            sealed class MyFilter : Microsoft.AspNetCore.Mvc.Filters.IActionFilter
            {
                public MyFilter(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsAnEndpointFilter()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            sealed class MyFilter : Microsoft.AspNetCore.Http.IEndpointFilter
            {
                public MyFilter(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsASignalRHub()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            sealed class MyHub : Microsoft.AspNetCore.SignalR.Hub
            {
                public MyHub(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsAnAuthorizationHandler()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            sealed class MyHandler : Microsoft.AspNetCore.Authorization.AuthorizationHandler<object>
            {
                public MyHandler(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsADelegatingHandler()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            abstract class MyHandler : System.Net.Http.DelegatingHandler
            {
                protected MyHandler(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheParameterIsNotTheAccessor()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpContextAccessorInServiceAnalyzer>(Stubs + """
            sealed class MyService
            {
                public MyService(string name) { }
            }
            """);
    }
}
