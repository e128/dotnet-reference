using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AntiforgeryValidationCodeFixTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task AddsTheAttributeAboveTheVerb()
    {
        return CodeFixTestRunner.RunAsync<AntiforgeryValidationAnalyzer, AntiforgeryValidationCodeFixProvider>(
            """
            using Microsoft.AspNetCore.Mvc;

            class HomeController
            {
                [HttpPost]
                public void {|E128242:Save|}() { }
            }
            """,
            """
            using Microsoft.AspNetCore.Mvc;

            class HomeController
            {
                [ValidateAntiForgeryToken]
                [HttpPost]
                public void Save() { }
            }
            """,
            options: new CodeFixTestOptions { Sources = [TestStubs.AspNetCoreMvc] });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AddsTheUsingWhenTheVerbIsFullyQualified()
    {
        return CodeFixTestRunner.RunAsync<AntiforgeryValidationAnalyzer, AntiforgeryValidationCodeFixProvider>(
            """
            class HomeController
            {
                [Microsoft.AspNetCore.Mvc.HttpPost]
                public void {|E128242:Save|}() { }
            }
            """,
            """
            using Microsoft.AspNetCore.Mvc;

            class HomeController
            {
                [ValidateAntiForgeryToken]
                [Microsoft.AspNetCore.Mvc.HttpPost]
                public void Save() { }
            }
            """,
            options: new CodeFixTestOptions { Sources = [TestStubs.AspNetCoreMvc] });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TwoActions_BothFixedByFixAll()
    {
        return CodeFixTestRunner.RunAsync<AntiforgeryValidationAnalyzer, AntiforgeryValidationCodeFixProvider>(
            """
            using Microsoft.AspNetCore.Mvc;

            class HomeController
            {
                [HttpPost]
                public void {|E128242:Save|}() { }

                [HttpPut]
                public void {|E128242:Update|}() { }
            }
            """,
            """
            using Microsoft.AspNetCore.Mvc;

            class HomeController
            {
                [ValidateAntiForgeryToken]
                [HttpPost]
                public void Save() { }

                [ValidateAntiForgeryToken]
                [HttpPut]
                public void Update() { }
            }
            """,
            options: new CodeFixTestOptions { Sources = [TestStubs.AspNetCoreMvc] });
    }
}
