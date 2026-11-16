using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LoggerArgumentGuardAnalyzerBranchCoverageMoreTests
{
    private static readonly ReferenceAssemblies Net100WithLogging = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.Logging.Abstractions", "10.0.13")]);

    private static Task VerifyAsync(string code, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>(
            code,
            expected: null,
            options ?? new AnalyzerTestOptions { References = Net100WithLogging });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFrameworkHasNoLoggerExtensions()
    {
        return VerifyAsync(
            """
            class C
            {
                void M()
                {
                }
            }
            """,
            new AnalyzerTestOptions());
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLogCallDoesNotBind()
    {
        return VerifyAsync(
            """
            class C
            {
                void M(object logger, int count)
                {
                    logger.LogInformation($"count {count}");
                }
            }
            """,
            new AnalyzerTestOptions { References = Net100WithLogging, CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardLivesInALocalFunctionOrIsAssignedLater()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, int count)
                {
                    bool Enabled() => logger.IsEnabled(LogLevel.Information);

                    if (Enabled())
                    {
                        {|E128122:logger.LogInformation($"count {count}")|};
                    }

                    bool later;
                    later = logger.IsEnabled(LogLevel.Warning);
                    if (later)
                    {
                        {|E128122:logger.LogWarning($"count {count}")|};
                    }
                }
            }
            """);
    }
}
