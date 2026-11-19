using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AntiforgeryValidationAnalyzerTests
{
    private static readonly AnalyzerTestOptions Options = new() { Sources = [TestStubs.AspNetCoreMvc] };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForHttpPostWithoutAntiforgery()
    {
        return AnalyzerTestRunner.VerifyAsync<AntiforgeryValidationAnalyzer>(
            """
            using Microsoft.AspNetCore.Mvc;

            class HomeController
            {
                [HttpPost]
                public void {|E128242:Save|}() { }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForHttpDeleteWithoutAntiforgery()
    {
        return AnalyzerTestRunner.VerifyAsync<AntiforgeryValidationAnalyzer>(
            """
            using Microsoft.AspNetCore.Mvc;

            class HomeController
            {
                [HttpDelete]
                public void {|E128242:Remove|}() { }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMethodValidatesTheToken()
    {
        return AnalyzerTestRunner.VerifyAsync<AntiforgeryValidationAnalyzer>(
            """
            using Microsoft.AspNetCore.Mvc;

            class HomeController
            {
                [HttpPost]
                [ValidateAntiForgeryToken]
                public void Save() { }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMethodUsesTheAutomaticAttribute()
    {
        return AnalyzerTestRunner.VerifyAsync<AntiforgeryValidationAnalyzer>(
            """
            using Microsoft.AspNetCore.Mvc;

            class HomeController
            {
                [HttpPost]
                [AutoValidateAntiforgeryToken]
                public void Save() { }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeUsesTheAutomaticAttribute()
    {
        return AnalyzerTestRunner.VerifyAsync<AntiforgeryValidationAnalyzer>(
            """
            using Microsoft.AspNetCore.Mvc;

            [AutoValidateAntiforgeryToken]
            class HomeController
            {
                [HttpPost]
                public void Save() { }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMethodIgnoresTheToken()
    {
        return AnalyzerTestRunner.VerifyAsync<AntiforgeryValidationAnalyzer>(
            """
            using Microsoft.AspNetCore.Mvc;

            class HomeController
            {
                [HttpPost]
                [IgnoreAntiforgeryToken]
                public void Save() { }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAReadOnlyVerb()
    {
        return AnalyzerTestRunner.VerifyAsync<AntiforgeryValidationAnalyzer>(
            """
            using Microsoft.AspNetCore.Mvc;

            class HomeController
            {
                [HttpGet]
                public void Index() { }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnApiController()
    {
        return AnalyzerTestRunner.VerifyAsync<AntiforgeryValidationAnalyzer>(
            """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            class ValuesController
            {
                [HttpPost]
                public void Save() { }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAUserTypeWithTheSameAttributeName()
    {
        return AnalyzerTestRunner.VerifyAsync<AntiforgeryValidationAnalyzer>(
            """
            class HttpPostAttribute : System.Attribute
            {
            }

            class C
            {
                [HttpPost]
                public void Save() { }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAssemblyHasNoMvcTypes()
    {
        return AnalyzerTestRunner.VerifyAsync<AntiforgeryValidationAnalyzer>("""
            class C
            {
                void M()
                {
                }
            }
            """);
    }
}
