using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LoggerArgumentGuardCodeFixBranchCoverageTests
{
    private static readonly ReferenceAssemblies Net100WithLogging = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.Logging.Abstractions", "10.0.13")]);

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Trace")]
    [InlineData("Debug")]
    [InlineData("Warning")]
    [InlineData("Error")]
    [InlineData("Critical")]
    public Task Fix_GuardsWithTheLevelOfTheLogMethod(string level)
    {
        var source = $$"""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, int count)
                {
                    {|E128122:logger.Log{{level}}($"count {count}")|};
                }
            }
            """;
        var fixedCode = $$"""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, int count)
                {
                    if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.{{level}}))
                    {
                        logger.Log{{level}}($"count {count}");
                    }
                }
            }
            """;

        return CodeFixTestRunner.RunAsync<LoggerArgumentGuardAnalyzer, LoggerArgumentGuardCodeFixProvider>(
            source,
            fixedCode,
            references: Net100WithLogging);
    }
}
