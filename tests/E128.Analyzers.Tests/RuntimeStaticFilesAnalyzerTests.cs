using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RuntimeStaticFilesAnalyzerTests
{
    private const string StubWithoutMapStaticAssets = """
                                                      namespace Microsoft.AspNetCore.Builder
                                                      {
                                                          public interface IApplicationBuilder
                                                          {
                                                          }

                                                          public static class StaticFileExtensions
                                                          {
                                                              public static IApplicationBuilder UseStaticFiles(this IApplicationBuilder app)
                                                              {
                                                                  return app;
                                                              }
                                                          }
                                                      }
                                                      """;

    private static readonly AnalyzerTestOptions Options = new() { Sources = [TestStubs.AspNetCoreMinimal] };

    private static DiagnosticResult Expected()
    {
        return new DiagnosticResult("E128230", DiagnosticSeverity.Warning).WithLocation(0);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheAppCallsUseStaticFiles()
    {
        return AnalyzerTestRunner.VerifyAsync<RuntimeStaticFilesAnalyzer>(
            """
            using Microsoft.AspNetCore.Builder;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    {|#0:app.UseStaticFiles()|};
                }
            }
            """,
            Expected(),
            Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFrameworkHasNoMapStaticAssets()
    {
        return AnalyzerTestRunner.VerifyAsync<RuntimeStaticFilesAnalyzer>(
            """
            using Microsoft.AspNetCore.Builder;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    app.UseStaticFiles();
                }
            }
            """,
            options: new AnalyzerTestOptions { Sources = [StubWithoutMapStaticAssets] });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallPassesStaticFileOptions()
    {
        return AnalyzerTestRunner.VerifyAsync<RuntimeStaticFilesAnalyzer>(
            """
            using Microsoft.AspNetCore.Builder;

            class C
            {
                void M(IApplicationBuilder app)
                {
                    app.UseStaticFiles(new StaticFileOptions());
                }
            }
            """,
            options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnotherTypeDeclaresUseStaticFiles()
    {
        return AnalyzerTestRunner.VerifyAsync<RuntimeStaticFilesAnalyzer>(
            """
            class App
            {
                public void UseStaticFiles()
                {
                }
            }

            class C
            {
                void M(App app)
                {
                    app.UseStaticFiles();
                }
            }
            """,
            options: Options);
    }
}
