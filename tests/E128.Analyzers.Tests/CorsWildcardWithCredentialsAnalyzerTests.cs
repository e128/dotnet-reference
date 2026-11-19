using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CorsWildcardWithCredentialsAnalyzerTests
{
    private static readonly AnalyzerTestOptions Options = new() { Sources = [TestStubs.AspNetCoreCors] };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnyOriginPrecedesCredentials()
    {
        return AnalyzerTestRunner.VerifyAsync<CorsWildcardWithCredentialsAnalyzer>(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            class C
            {
                void M()
                {
                    var builder = new CorsPolicyBuilder();
                    {|E128241:builder.AllowAnyOrigin()|}.AllowCredentials();
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCredentialsPrecedeAnyOrigin()
    {
        return AnalyzerTestRunner.VerifyAsync<CorsWildcardWithCredentialsAnalyzer>(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            class C
            {
                void M()
                {
                    var builder = new CorsPolicyBuilder();
                    {|E128241:builder.AllowCredentials().AllowAnyOrigin()|};
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOnlyAnyOriginIsSet()
    {
        return AnalyzerTestRunner.VerifyAsync<CorsWildcardWithCredentialsAnalyzer>(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            class C
            {
                void M()
                {
                    var builder = new CorsPolicyBuilder();
                    builder.AllowAnyOrigin();
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOnlyCredentialsAreSet()
    {
        return AnalyzerTestRunner.VerifyAsync<CorsWildcardWithCredentialsAnalyzer>(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            class C
            {
                void M()
                {
                    var builder = new CorsPolicyBuilder();
                    builder.AllowCredentials();
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNamedOriginsAreSetWithCredentials()
    {
        return AnalyzerTestRunner.VerifyAsync<CorsWildcardWithCredentialsAnalyzer>(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            class C
            {
                void M()
                {
                    var builder = new CorsPolicyBuilder();
                    builder.WithOrigins("https://example.test").AllowCredentials();
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAUserTypeWithTheSameMethodNames()
    {
        return AnalyzerTestRunner.VerifyAsync<CorsWildcardWithCredentialsAnalyzer>(
            """
            class PolicyBuilder
            {
                public PolicyBuilder AllowAnyOrigin() => this;
                public PolicyBuilder AllowCredentials() => this;
            }

            class C
            {
                void M()
                {
                    var builder = new PolicyBuilder();
                    builder.AllowAnyOrigin().AllowCredentials();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAssemblyHasNoCorsBuilder()
    {
        return AnalyzerTestRunner.VerifyAsync<CorsWildcardWithCredentialsAnalyzer>("""
            class C
            {
                void M()
                {
                }
            }
            """);
    }
}
