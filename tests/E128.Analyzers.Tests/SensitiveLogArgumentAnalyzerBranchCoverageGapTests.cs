using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SensitiveLogArgumentAnalyzerBranchCoverageGapTests
{
    private static readonly ReferenceAssemblies Net100WithLogging = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.Logging.Abstractions", "10.0.13")]);

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<SensitiveLogArgumentAnalyzer>(
            code,
            options: new AnalyzerTestOptions { References = Net100WithLogging });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForParamsArraysWithoutAnInitializer()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, object[] values)
                {
                    logger.LogInformation("a {A}", new object[2]);
                    logger.LogInformation("b {A}", values);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ElementsOfAnArrayThatTheCompilerConverts()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, string password, string user)
                {
                    logger.LogInformation("a {A} {B}", new string[] { user, {|E128166:password|} });
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFormatterDelegateCarriesASensitiveName()
    {
        return VerifyAsync("""
            using System;
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, string payload, Func<string, Exception?, string> passwordFormatter)
                {
                    logger.Log(LogLevel.Information, new EventId(1), payload, null, passwordFormatter);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForExpressionsThatKeepNoName()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, string password, int counter)
                {
                    logger.LogInformation("a {A}", password + "!");
                    logger.LogInformation("b {A}", counter++);
                    logger.LogInformation("c {A}", -counter);
                    logger.LogInformation("d {A}", new object());
                    logger.LogInformation("e {A}", "literal");
                    logger.LogInformation("f {A}", this);
                }
            }
            """);
    }
}
