using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class LoggerArgumentGuardBranchCoverageTests
{
    private static readonly ReferenceAssemblies Net100WithLogging = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("Microsoft.Extensions.Logging.Abstractions", "10.0.13")]);

    private static AnalyzerTestOptions Options => new() { References = Net100WithLogging };

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<LoggerArgumentGuardAnalyzer>(code, options: Options);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("LogTrace")]
    [InlineData("LogDebug")]
    [InlineData("LogInformation")]
    [InlineData("LogWarning")]
    [InlineData("LogError")]
    [InlineData("LogCritical")]
    public Task Reports_ForEveryNamedLogLevel(string method)
    {
        return VerifyAsync($$"""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, int count)
                {
                    {|E128122:logger.{{method}}($"count {count}")|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheMethodIsNotALevelMethod()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, int count)
                {
                    logger.Log(LogLevel.Information, $"count {count}");
                    _ = logger.BeginScope($"count {count}");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLevelMethodDoesNotComeFromLoggerExtensions()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;

            static class OwnExtensions
            {
                public static void LogInformation(this ILogger logger, string message)
                {
                }
            }

            class C
            {
                void M(ILogger logger, int count)
                {
                    logger.LogInformation($"count {count}");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheInterpolatedMessageIsConstant()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger)
                {
                    logger.LogInformation($"fixed text");
                    logger.LogInformation("a" + "b");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenArgumentsAreNotMessageBuilders()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, int count)
                {
                    logger.LogInformation("count {Count}", count > 3);
                    logger.LogInformation("count {Count}", count - 1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAHelperMethodHoldsTheGuard()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                static bool ExpressionHelper(ILogger logger) => logger.IsEnabled(LogLevel.Information);

                static bool BlockHelper(ILogger logger)
                {
                    return logger.IsEnabled(LogLevel.Warning);
                }

                void M(ILogger logger, int count)
                {
                    if (ExpressionHelper(logger))
                    {
                        logger.LogInformation($"count {count}");
                    }

                    if (BlockHelper(logger))
                    {
                        logger.LogWarning($"count {count}");
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAPropertyAccessorHoldsTheGuard()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                private readonly ILogger _logger;

                C(ILogger logger)
                {
                    _logger = logger;
                }

                private bool BlockEnabled
                {
                    get
                    {
                        return _logger.IsEnabled(LogLevel.Information);
                    }
                }

                private bool ArrowEnabled
                {
                    get => _logger.IsEnabled(LogLevel.Warning);
                }

                void M(int count)
                {
                    if (BlockEnabled)
                    {
                        _logger.LogInformation($"count {count}");
                    }

                    if (ArrowEnabled)
                    {
                        _logger.LogWarning($"count {count}");
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAPropertyWithoutAccessorBodyIsTheCondition()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                private bool Enabled { get; set; }

                void M(ILogger logger, int count)
                {
                    if (Enabled)
                    {
                        {|E128122:logger.LogInformation($"count {count}")|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAFieldHoldsTheCondition()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                private bool _enabled;

                void M(ILogger logger, int count)
                {
                    if (_enabled)
                    {
                        {|E128122:logger.LogInformation($"count {count}")|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardIsNegatedInsideTheThenBranch()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, int count)
                {
                    if (!logger.IsEnabled(LogLevel.Information))
                    {
                        {|E128122:logger.LogInformation($"count {count}")|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLevelIsNamedWithAStaticImport()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            using static Microsoft.Extensions.Logging.LogLevel;
            class C
            {
                void M(ILogger logger, int count)
                {
                    if (logger.IsEnabled(Information))
                    {
                        logger.LogInformation($"count {count}");
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheGuardLevelIsNotANamedLevel()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                static LogLevel Pick() => LogLevel.Information;

                void M(ILogger logger, ILogger other, LogLevel level, int count)
                {
                    if (logger.IsEnabled(level))
                    {
                        {|E128122:logger.LogInformation($"count {count}")|};
                    }

                    if (logger.IsEnabled(Pick()))
                    {
                        {|E128122:logger.LogInformation($"count {count}")|};
                    }

                    if (other.IsEnabled(LogLevel.Information))
                    {
                        {|E128122:logger.LogInformation($"count {count}")|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAParenthesizedNegatedGuardReturnsEarly()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, int count)
                {
                    if (!(logger.IsEnabled(LogLevel.Information))) return;

                    logger.LogInformation($"count {count}");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenEarlierStatementsAreNotAnEarlyReturnGuard()
    {
        return VerifyAsync("""
            using Microsoft.Extensions.Logging;
            class C
            {
                void M(ILogger logger, ILogger other, int count, bool flag)
                {
                    if (flag)
                    {
                        return;
                    }

                    if (!flag)
                    {
                        return;
                    }

                    if (!logger.IsEnabled(LogLevel.Information))
                    {
                        count++;
                    }

                    if (!logger.IsEnabled(LogLevel.Information))
                    {
                        count++;
                        return;
                    }

                    if (!other.IsEnabled(LogLevel.Information))
                    {
                        return;
                    }

                    if (!logger.IsEnabled(LogLevel.Warning))
                    {
                        return;
                    }

                    {|E128122:logger.LogInformation($"count {count}")|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCallIsInsideALambdaInAFieldInitializer()
    {
        return VerifyAsync("""
            using System;
            using Microsoft.Extensions.Logging;
            class C
            {
                private readonly ILogger _logger = null!;

                private Action<int> Callback => count => {|E128122:_logger.LogInformation($"count {count}")|};
            }
            """);
    }
}
